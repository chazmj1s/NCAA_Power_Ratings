using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Text.Json;
using System.Windows.Input;
using NCAA_Power_Ratings.Mobile.Helpers;
using NCAA_Power_Ratings.Mobile.Models;
using NCAA_Power_Ratings.Mobile.Services;

namespace NCAA_Power_Ratings.Mobile.ViewModels
{
    public class ProjectionsViewModel : BaseViewModel
    {
        private readonly GameDataApiService _apiService;

        private bool   _isBusy;
        private int    _selectedYear    = 2025;
        private int    _selectedWeek    = 15;
        private string _selectedView    = "Standings";   // "Standings" | "Championship"
        private string _activeConference = "All";
        private string _selectedConference = "All";
        private string _statusMessage   = string.Empty;

        // Raw data from API
        private List<ProjectedTeamStanding>  _allStandings     = new();
        private List<ChampionshipMatchup>    _allChampionships = new();

        public ProjectionsViewModel(GameDataApiService apiService, FollowService followService)
            : base(followService)
        {
            _apiService = apiService;

            LoadDataCommand = new Microsoft.Maui.Controls.Command(async () => await LoadDataAsync());

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

            SelectWeekCommand = new Microsoft.Maui.Controls.Command<int>(OnWeekSelected);

            SelectViewCommand = new Microsoft.Maui.Controls.Command<string>(view =>
            {
                SelectedView = view;
            });

            SelectConferenceCommand = new Microsoft.Maui.Controls.Command(async () =>
            {
                var fbsConferences = new List<string> { "All" };
                fbsConferences.AddRange(ConferenceHelper.OrderedConferences.Select(c => c.Display));

                var result = await Shell.Current.DisplayActionSheet(
                    "Filter by Conference", "Cancel", null, fbsConferences.ToArray());

                if (result != null && result != "Cancel")
                {
                    _activeConference  = result;
                    SelectedConference = result;
                    ApplyConferenceFilter();
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

            ToggleTeamExpandCommand = new Microsoft.Maui.Controls.Command<ProjectedTeamStanding>(team =>
            {
                if (team != null) team.IsExpanded = !team.IsExpanded;
            });

            ToggleMatchupExpandCommand = new Microsoft.Maui.Controls.Command<ChampionshipMatchup>(matchup =>
            {
                if (matchup != null) matchup.IsExpanded = !matchup.IsExpanded;
            });

            ToggleContendersExpandCommand = new Microsoft.Maui.Controls.Command<ChampionshipMatchup>(matchup =>
            {
                if (matchup != null) matchup.IsContendersExpanded = !matchup.IsContendersExpanded;
            });
        }

        // ── Bindable collections ──────────────────────────────────────────

        public ObservableCollection<WeekItem>             Weeks         { get; } = new();
        public ObservableCollection<ProjectedTeamStanding> Standings    { get; } = new();
        public ObservableCollection<ChampionshipMatchup>  Championships { get; } = new();

        // ── Bindable properties ───────────────────────────────────────────

        public bool IsBusy
        {
            get => _isBusy;
            set { _isBusy = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsLoading)); }
        }

        public bool IsLoading => _isBusy;
        public bool HasLoaded { get; private set; }

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

        public string SelectedView
        {
            get => _selectedView;
            set
            {
                _selectedView = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsStandingsView));
                OnPropertyChanged(nameof(IsChampionshipView));
            }
        }

        public bool IsStandingsView    => _selectedView == "Standings";
        public bool IsChampionshipView => _selectedView == "Championship";

        public string SelectedConference
        {
            get => _selectedConference;
            set { _selectedConference = value; OnPropertyChanged(); }
        }

        // ── Commands ──────────────────────────────────────────────────────

        public ICommand LoadDataCommand          { get; }
        public ICommand SelectYearCommand        { get; }
        public ICommand SelectWeekCommand        { get; }
        public ICommand SelectViewCommand        { get; }
        public ICommand SelectConferenceCommand  { get; }
        public ICommand PreviousWeekCommand      { get; }
        public ICommand NextWeekCommand          { get; }
        public ICommand ToggleTeamExpandCommand  { get; }
        public ICommand ToggleMatchupExpandCommand { get; }
        public ICommand ToggleContendersExpandCommand { get; }

        // ── Load ──────────────────────────────────────────────────────────

        public async Task LoadDataAsync()
        {
            if (IsBusy) return;
            IsBusy = true;
            StatusMessage = "Loading projections...";

            try
            {
                // Load both endpoints in parallel
                var standingsTask      = _apiService.GetProjectedStandingsAsync(_selectedYear, _selectedWeek);
                var championshipsTask  = _apiService.GetProjectedChampionshipQualifiersAsync(_selectedYear, _selectedWeek);

                await Task.WhenAll(standingsTask, championshipsTask);

                var standings     = standingsTask.Result;
                var championships = championshipsTask.Result;

                if (standings == null || championships == null)
                {
                    StatusMessage = "Failed to load projections";
                    return;
                }

                // ── Build week list from standings data ───────────────────
                // Use weeks 1-15 as the range
                var weekNums = Enumerable.Range(1, 15).ToList();
                Weeks.Clear();
                foreach (var w in weekNums)
                    Weeks.Add(new WeekItem { Week = w, IsSelected = w == _selectedWeek });

                // ── Process standings ─────────────────────────────────────
                // Compute projected finish rank within each conference
                var byConference = standings
                    .GroupBy(s => s.Conference)
                    .ToDictionary(g => g.Key, g => g
                        .OrderByDescending(s => s.ProjectedWinPct)
                        .ThenByDescending(s => s.ProjectedWins)
                        .ToList());

                foreach (var conf in byConference.Values)
                    for (int i = 0; i < conf.Count; i++)
                        conf[i].ProjectedFinish = i + 1;

                _allStandings = standings;
                _allChampionships = championships;

                ApplyConferenceFilter();

                // Refresh championships (no conference filter)
                Championships.Clear();
                foreach (var c in championships)
                    Championships.Add(c);

                StatusMessage = $"Week {_selectedWeek} projections";
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

        // ── Week selection ────────────────────────────────────────────────

        private void OnWeekSelected(int week)
        {
            _selectedWeek = week;
            foreach (var w in Weeks) w.IsSelected = w.Week == _selectedWeek;
            OnPropertyChanged(nameof(SelectedWeek));
            _ = LoadDataAsync();  // reload with new throughWeek
        }

        // ── Conference filter (Standings only) ────────────────────────────

        private void ApplyConferenceFilter()
        {
            var filtered = _activeConference == "All"
                ? _allStandings
                : _allStandings.Where(s =>
                {
                    var abbr = ConferenceHelper.DisplayToAbbr(_activeConference);
                    return s.Conference.Equals(abbr, StringComparison.OrdinalIgnoreCase);
                }).ToList();

            Standings.Clear();
            foreach (var s in filtered)
                Standings.Add(s);
        }
    }
}
