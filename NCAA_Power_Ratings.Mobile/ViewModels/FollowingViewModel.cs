using System.Collections.ObjectModel;
using System.Windows.Input;
using NCAA_Power_Ratings.Mobile.Helpers;
using NCAA_Power_Ratings.Mobile.Models;
using NCAA_Power_Ratings.Mobile.Services;

namespace NCAA_Power_Ratings.Mobile.ViewModels
{
    public class FollowingViewModel : BaseViewModel
    {
        private readonly GameDataApiService _apiService;

        // ── Raw data ──────────────────────────────────────────────────────
        private List<TeamInfo>    _allTeams     = [];
        private List<RivalryInfo> _allRivalries = [];

        // ── Sub-tab state ─────────────────────────────────────────────────
        private string _selectedView = "Teams";

        public string SelectedView
        {
            get => _selectedView;
            set
            {
                if (_selectedView == value) return;
                _selectedView = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsTeamsView));
                OnPropertyChanged(nameof(IsRivalriesView));
            }
        }

        public bool IsTeamsView     => _selectedView == "Teams";
        public bool IsRivalriesView => _selectedView == "Rivalries";

        // ── Shared state ──────────────────────────────────────────────────
        private bool   _isBusy;
        private string _statusMessage = string.Empty;

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

        // ── Teams ─────────────────────────────────────────────────────────
        public ObservableCollection<TeamInfo>  Teams            { get; } = new();
        public ObservableCollection<string>    ConferenceFilters { get; } = new();

        private string _selectedConference = "All";
        public string SelectedConference
        {
            get => _selectedConference;
            set
            {
                if (_selectedConference == value) return;
                _selectedConference = value;
                OnPropertyChanged();
                ApplyTeamFilter();
            }
        }

        // ── Rivalries ─────────────────────────────────────────────────────
        public ObservableCollection<RivalryInfo> Rivalries { get; } = new();

        public List<string> TierFilters { get; } =
            ["All", "🔥 Epic", "⭐ National", "🏠 Regional", "• Meh"];

        private string _selectedTier = "All";
        public string SelectedTier
        {
            get => _selectedTier;
            set
            {
                if (_selectedTier == value) return;
                _selectedTier = value;
                OnPropertyChanged();
                ApplyRivalryFilter();
            }
        }

        // ── Commands ──────────────────────────────────────────────────────
        public ICommand LoadDataCommand   { get; }
        public ICommand SelectViewCommand { get; }

        // ── Constructor ───────────────────────────────────────────────────
        public FollowingViewModel(
            GameDataApiService apiService,
            FollowService followService)
            : base(followService)
        {
            _apiService = apiService;

            LoadDataCommand = new Microsoft.Maui.Controls.Command(
                async () => await LoadDataAsync());

            SelectViewCommand = new Microsoft.Maui.Controls.Command<string>(view =>
            {
                SelectedView = view;
            });

            _followService.TeamFollowChanged += OnTeamFollowChanged;
        }

        // ── Load ──────────────────────────────────────────────────────────
        public async Task LoadDataAsync()
        {
            if (IsBusy) return;
            IsBusy = true;
            StatusMessage = "Loading...";

            try
            {
                var teamsTask     = _apiService.GetTeamsAsync();
                var rivalriesTask = _apiService.GetNamedRivalriesAsync();

                await Task.WhenAll(teamsTask, rivalriesTask);

                // ── Teams ─────────────────────────────────────────────────
                var teams = teamsTask.Result;
                if (teams != null && teams.Count > 0)
                {
                    foreach (var t in teams)
                        t.IsFollowed = _followService.IsFollowed(t.TeamID);

                    _allTeams = [.. teams.OrderBy(t => t.TeamName)];

                    ConferenceFilters.Clear();
                    foreach (var c in ConferenceHelper.FilterDisplayList())
                        ConferenceFilters.Add(c);

                    _selectedConference = "All";
                    OnPropertyChanged(nameof(SelectedConference));

                    ApplyTeamFilter();
                }

                // ── Rivalries ─────────────────────────────────────────────
                var rivalries = rivalriesTask.Result;
                if (rivalries != null && rivalries.Count > 0)
                {
                    var followedIds = _followService.GetFollowedIds();
                    foreach (var r in rivalries)
                    {
                        r.Team1IsFollowed = followedIds.Contains(r.Team1Id);
                        r.Team2IsFollowed = followedIds.Contains(r.Team2Id);
                    }

                    _allRivalries = [.. rivalries
                        .OrderBy(r => TierSortOrder(r.RivalryTier))
                        .ThenBy(r => r.RivalryName)];

                    ApplyRivalryFilter();
                }

                StatusMessage = string.Empty;
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

        // ── Filters ───────────────────────────────────────────────────────
        private void ApplyTeamFilter()
        {
            var filtered = _allTeams.AsEnumerable();

            if (SelectedConference != "All")
            {
                var abbr = ConferenceHelper.DisplayToAbbr(SelectedConference);
                filtered = filtered.Where(t => t.ConferenceAbbr == abbr);
            }

            var sorted = filtered
                .OrderByDescending(t => t.IsFollowed)
                .ThenBy(t => t.TeamName);

            Teams.Clear();
            foreach (var t in sorted)
                Teams.Add(t);
        }

        private void ApplyRivalryFilter()
        {
            var filtered = _allRivalries.AsEnumerable();

            filtered = _selectedTier switch
            {
                "🔥 Epic"     => filtered.Where(r => r.RivalryTier == "EPIC"),
                "⭐ National" => filtered.Where(r => r.RivalryTier == "NATIONAL"),
                "🏠 Regional" => filtered.Where(r => r.RivalryTier == "REGIONAL"),
                "• Meh"       => filtered.Where(r => r.RivalryTier == "MEH"),
                _             => filtered
            };

            Rivalries.Clear();
            foreach (var r in filtered)
                Rivalries.Add(r);
        }

        private void OnTeamFollowChanged(int teamId, bool isFollowed)
        {
            var team = _allTeams.FirstOrDefault(t => t.TeamID == teamId);
            if (team != null)
            {
                team.IsFollowed = isFollowed;
                ApplyTeamFilter();
            }

            foreach (var r in _allRivalries)
            {
                if (r.Team1Id == teamId) r.Team1IsFollowed = isFollowed;
                if (r.Team2Id == teamId) r.Team2IsFollowed = isFollowed;
            }
            ApplyRivalryFilter();
        }

        private static int TierSortOrder(string? tier) => tier switch
        {
            "EPIC"     => 0,
            "NATIONAL" => 1,
            "REGIONAL" => 2,
            "MEH"      => 3,
            _          => 4
        };
    }
}
