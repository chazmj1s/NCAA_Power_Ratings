using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
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
        private ObservableCollection<GameResult> _filteredGames = new();
        private ObservableCollection<string> _teamNames = new();
        private bool _isBusy;
        private int _selectedYear = 2025;
        private string _activeFilter = "All";
        private string _activeTeamFilter = "All Teams";
        private string _statusMessage = "Loading...";
        private string _sortColumn = "Rk";
        private bool _sortAscending = true;

        public ScheduleViewModel(GameDataApiService apiService, FollowService followService)
    : base(followService)
        {
            _apiService = apiService;
            LoadDataCommand  = new Microsoft.Maui.Controls.Command(async () => await LoadDataAsync());
            RefreshCommand   = new Microsoft.Maui.Controls.Command(async () => await LoadDataAsync());
            SortColumnCommand = new Microsoft.Maui.Controls.Command<string>(SortByColumn);
            
            _followService.TeamFollowChanged += OnTeamFollowChanged;
        }

        public ObservableCollection<GameResult> FilteredGames
        {
            get => _filteredGames;
            set { _filteredGames = value; OnPropertyChanged(); }
        }

        public ObservableCollection<string> TeamNames
        {
            get => _teamNames;
            set { _teamNames = value; OnPropertyChanged(); }
        }

        public bool IsBusy
        {
            get => _isBusy;
            set { _isBusy = value; OnPropertyChanged(); }
        }

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

        public ICommand LoadDataCommand  { get; }
        public ICommand RefreshCommand   { get; }
        public ICommand SortColumnCommand { get; }

        public async Task LoadDataAsync()
        {
            if (IsBusy) return;
            IsBusy = true;
            StatusMessage = "Loading schedule...";

            try
            {
                var games = await _apiService.GetScheduleAsync(_selectedYear);
                if (games != null)
                {
                    // Stamp original sequence numbers
                    for (int i = 0; i < games.Count; i++)
                        games[i].SequenceNumber = i + 1;

                    _allGames = games;

                    var followedIds = _followService.GetFollowedIds();
                    foreach (var g in _allGames)
                    {
                        g.WinnerIsFollowed = followedIds.Contains(g.WinnerId);
                        g.LoserIsFollowed = followedIds.Contains(g.LoserId);
                    }

                    // Build sorted team list from this season's participants
                    var names = games
                        .SelectMany(g => new[] { g.WinnerName, g.LoserName })
                        .Distinct()
                        .OrderBy(n => n)
                        .ToList();

                    names.Insert(0, "All Teams");
                    TeamNames = new ObservableCollection<string>(names);

                    _sortColumn = "Rk";
                    _sortAscending = true;
                    ApplyFiltersAndSort();
                    StatusMessage = $"{_allGames.Count} games";
                }
                else
                {
                    StatusMessage = "Failed to load schedule";
                }
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

        public void ApplyFilter(string filterType)
        {
            _activeFilter = filterType;
            ApplyFiltersAndSort();
        }

        public void ApplyTeamFilter(string teamName)
        {
            _activeTeamFilter = teamName;
            ApplyFiltersAndSort();
        }

        public void SortByColumn(string column)
        {
            if (_sortColumn == column)
                _sortAscending = !_sortAscending;
            else
            {
                _sortColumn = column;
                _sortAscending = true;
            }
            ApplyFiltersAndSort();
        }

        private void ApplyFiltersAndSort()
        {
            IEnumerable<GameResult> filtered = _allGames;

            // Conference / tier filter
            filtered = _activeFilter switch
            {
                "All"      => filtered,
                "P4"       => filtered.Where(g => g.WinnerTier == "P4" || g.LoserTier == "P4"),
                "G5"       => filtered.Where(g => g.WinnerTier == "G5" || g.LoserTier == "G5"),
                "── Conf ──" => filtered,
                _          => filtered.Where(g =>
                {
                    var abbr = ConferenceHelper.DisplayToAbbr(_activeFilter);
                    return g.WinnerConf.Equals(abbr, StringComparison.OrdinalIgnoreCase) ||
                           g.LoserConf.Equals(abbr,  StringComparison.OrdinalIgnoreCase);
                })
            };

            // Team filter
            if (_activeTeamFilter != "All Teams")
                filtered = filtered.Where(g =>
                    g.WinnerName.Equals(_activeTeamFilter, StringComparison.OrdinalIgnoreCase) ||
                    g.LoserName.Equals(_activeTeamFilter,  StringComparison.OrdinalIgnoreCase));

            // Sort
            filtered = _sortColumn switch
            {
                "Winner" => _sortAscending
                    ? filtered.OrderBy(g => g.WinnerName)
                    : filtered.OrderByDescending(g => g.WinnerName),
                "Loser"  => _sortAscending
                    ? filtered.OrderBy(g => g.LoserName)
                    : filtered.OrderByDescending(g => g.LoserName),
                _        => _sortAscending          // "Rk" = original order
                    ? filtered.OrderBy(g => g.SequenceNumber)
                    : filtered.OrderByDescending(g => g.SequenceNumber)
            };

            FilteredGames = new ObservableCollection<GameResult>(filtered);
        }

        private void OnTeamFollowChanged(int teamId, bool isFollowed)
        {
            foreach (var g in _allGames)
            {
                if (g.WinnerId == teamId) g.WinnerIsFollowed = isFollowed;
                if (g.LoserId == teamId) g.LoserIsFollowed = isFollowed;
            }

            MainThread.BeginInvokeOnMainThread(() =>
            {
                var temp = FilteredGames;
                FilteredGames = null;
                FilteredGames = temp;
            });
        }
    }
}
