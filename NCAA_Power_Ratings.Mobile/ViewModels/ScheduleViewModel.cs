using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
using NCAA_Power_Ratings.Mobile.Helpers;
using NCAA_Power_Ratings.Mobile.Models;
using NCAA_Power_Ratings.Mobile.Services;

namespace NCAA_Power_Ratings.Mobile.ViewModels
{
    public class ScheduleViewModel : BaseViewModel
    {
        private readonly GameDataApiService _apiService;
        private List<GameResult> _allGames = new();
        private ObservableCollection<GameResult> _games = new();
        private bool _isBusy;
        private int _selectedYear = 2025;
        private int _selectedWeek = 1;
        private string _activeFilter = "All";
        private string _selectedFilter = "All";
        private string _statusMessage = string.Empty;

        public ScheduleViewModel(GameDataApiService apiService, FollowService followService)
            : base(followService)
        {
            _apiService = apiService;

            LoadDataCommand = new Microsoft.Maui.Controls.Command(async () => await LoadDataAsync());
            RefreshCommand = new Microsoft.Maui.Controls.Command(async () => await LoadDataAsync());
            SelectWeekCommand = new Microsoft.Maui.Controls.Command<int>(OnWeekSelected);

            SelectYearCommand = new Microsoft.Maui.Controls.Command(async () =>
            {
                var years = Enumerable.Range(1965, 2025 - 1965 + 1)
                    .Select(y => y.ToString())
                    .Reverse()
                    .ToArray();

                var result = await Shell.Current.DisplayActionSheet(
                    "Select Year", "Cancel", null, years);

                if (result != null && result != "Cancel" && int.TryParse(result, out int year))
                    SelectedYear = year;
            });

            SelectFilterCommand = new Microsoft.Maui.Controls.Command(async () =>
            {
                var options = new List<string> { "All", "Followed", "P4", "G5", "── Conf ──" };
                options.AddRange(ConferenceHelper.OrderedConferences.Select(c => c.Display));

                var result = await Shell.Current.DisplayActionSheet(
                    "Filter", "Cancel", null, options.ToArray());

                if (result != null && result != "Cancel" && !result.StartsWith("──"))
                {
                    _activeFilter = result;
                    SelectedFilter = result;
                    ApplyFiltersAndSort();
                }
            });

            PreviousWeekCommand = new Microsoft.Maui.Controls.Command(() =>
            {
                var idx = Weeks.ToList().FindIndex(w => w.Week == _selectedWeek);
                if (idx > 0) OnWeekSelected(Weeks[idx - 1].Week);
            });

            NextWeekCommand = new Microsoft.Maui.Controls.Command(() =>
            {
                var idx = Weeks.ToList().FindIndex(w => w.Week == _selectedWeek);
                if (idx < Weeks.Count - 1) OnWeekSelected(Weeks[idx + 1].Week);
            });

            _followService.TeamFollowChanged += OnTeamFollowChanged;
        }

        // ── Bindable collections ──────────────────────────────────────

        public ObservableCollection<GameResult> Games
        {
            get => _games;
            set { _games = value; OnPropertyChanged(); }
        }

        public ObservableCollection<WeekItem> Weeks { get; } = new();

        // ── Bindable properties ───────────────────────────────────────

        public bool IsBusy
        {
            get => _isBusy;
            set { _isBusy = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsLoading)); }
        }

        public bool IsLoading => _isBusy;

        public string StatusMessage
        {
            get => _statusMessage;
            set { _statusMessage = value; OnPropertyChanged(); }
        }

        public int SelectedYear
        {
            get => _selectedYear;
            set
            {
                if (_selectedYear != value)
                {
                    _selectedYear = value;
                    OnPropertyChanged();
                    _ = LoadDataAsync();
                }
            }
        }

        public int SelectedWeek
        {
            get => _selectedWeek;
            set { _selectedWeek = value; OnPropertyChanged(); }
        }

        public string SelectedFilter
        {
            get => _selectedFilter;
            set { _selectedFilter = value; OnPropertyChanged(); }
        }
        public bool HasLoaded { get; private set; }


        // ── Commands ──────────────────────────────────────────────────

        public ICommand LoadDataCommand { get; }
        public ICommand RefreshCommand { get; }
        public ICommand SelectWeekCommand { get; }
        public ICommand SelectYearCommand { get; }
        public ICommand SelectFilterCommand { get; }
        public ICommand PreviousWeekCommand { get; }
        public ICommand NextWeekCommand { get; }

        // ── Load ──────────────────────────────────────────────────────

        public async Task LoadDataAsync()
        {
            if (IsBusy) return;
            IsBusy = true;
            StatusMessage = "Loading...";

            try
            {
                var games = await _apiService.GetScheduleAsync(_selectedYear);
                if (games == null || games.Count == 0)
                {
                    StatusMessage = "No games found";
                    return;
                }

                // Stamp sequence numbers
                for (int i = 0; i < games.Count; i++)
                    games[i].SequenceNumber = i + 1;

                _allGames = games;

                // Sync follow state
                var followedIds = _followService.GetFollowedIds();
                foreach (var g in _allGames)
                {
                    g.WinnerIsFollowed = followedIds.Contains(g.WinnerId);
                    g.LoserIsFollowed = followedIds.Contains(g.LoserId);
                }

                // Build week list
                var weeks = games.Select(g => g.Week).Distinct().OrderBy(w => w).ToList();
                Weeks.Clear();
                foreach (var w in weeks) Weeks.Add(new WeekItem { Week = w });

                // Default to most recent week with played games
                var lastPlayedWeek = games
                    .Where(g => g.IsPlayed)
                    .Select(g => g.Week)
                    .DefaultIfEmpty(weeks.First())
                    .Max();

                _selectedWeek = lastPlayedWeek;
                OnPropertyChanged(nameof(SelectedWeek));
                foreach (var w in Weeks) w.IsSelected = w.Week == _selectedWeek;

                ApplyFiltersAndSort();
                StatusMessage = "( ) = projected value";
                
                HasLoaded = true;
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        // ── Filter / sort ─────────────────────────────────────────────

        private void OnWeekSelected(int week)
        {
            _selectedWeek = week;
            foreach (var w in Weeks) w.IsSelected = w.Week == _selectedWeek;
            OnPropertyChanged(nameof(SelectedWeek));
            ApplyFiltersAndSort();
        }

        private void ApplyFiltersAndSort()
        {
            IEnumerable<GameResult> filtered = _allGames;

            // Week filter
            filtered = filtered.Where(g => g.Week == _selectedWeek);

            // Tier / conference / followed filter
            filtered = _activeFilter switch
            {
                "All" => filtered,
                "Followed" => filtered.Where(g => g.WinnerIsFollowed || g.LoserIsFollowed),
                "P4" => filtered.Where(g => g.WinnerTier == "P4" || g.LoserTier == "P4"),
                "G5" => filtered.Where(g => g.WinnerTier == "G5" || g.LoserTier == "G5"),
                _ => filtered.Where(g =>
                {
                    var abbr = ConferenceHelper.DisplayToAbbr(_activeFilter);
                    return g.WinnerConf.Equals(abbr, StringComparison.OrdinalIgnoreCase) ||
                           g.LoserConf.Equals(abbr, StringComparison.OrdinalIgnoreCase);
                })
            };

            // Sort by original sequence (preserves date/game order within week)
            var sorted = filtered.OrderBy(g => g.SequenceNumber).ToList();

            // Stamp ShowGroupHeader — true for first game of each date group
            string lastHeader = null;
            foreach (var g in sorted)
            {
                g.ShowGroupHeader = g.GroupHeader != lastHeader;
                lastHeader = g.GroupHeader;
            }

            Games = new ObservableCollection<GameResult>(sorted);
        }

        // ── Follow sync ───────────────────────────────────────────────

        private void OnTeamFollowChanged(int teamId, bool isFollowed)
        {
            foreach (var g in _allGames)
            {
                if (g.WinnerId == teamId) g.WinnerIsFollowed = isFollowed;
                if (g.LoserId == teamId) g.LoserIsFollowed = isFollowed;
            }

            MainThread.BeginInvokeOnMainThread(() =>
            {
                if (_activeFilter == "Followed")
                    ApplyFiltersAndSort();
                else
                {
                    var temp = Games;
                    Games = null;
                    Games = temp;
                }
            });
        }
    }

    // ── Week selector item ────────────────────────────────────────────

    public class WeekItem : INotifyPropertyChanged
    {
        private bool _isSelected;

        public int Week { get; init; }
        public string Label => $"Wk{Week}";

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                _isSelected = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }
}