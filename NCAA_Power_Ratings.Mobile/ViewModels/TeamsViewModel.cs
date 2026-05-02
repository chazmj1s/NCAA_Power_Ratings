using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using NCAA_Power_Ratings.Mobile.Helpers;
using NCAA_Power_Ratings.Mobile.Models;
using NCAA_Power_Ratings.Mobile.Services;

namespace NCAA_Power_Ratings.Mobile.ViewModels
{
    public class TeamsViewModel : INotifyPropertyChanged
    {
        private readonly GameDataApiService _apiService;
        private List<TeamInfo> _allTeams = [];

        private ObservableCollection<TeamInfo> _teams = [];
        public ObservableCollection<TeamInfo> Teams
        {
            get => _teams;
            private set { _teams = value; OnPropertyChanged(); }
        }

        private ObservableCollection<string> _conferenceFilters = [];
        public ObservableCollection<string> ConferenceFilters
        {
            get => _conferenceFilters;
            private set { _conferenceFilters = value; OnPropertyChanged(); }
        }

        private string _selectedConference = "All";
        public string SelectedConference
        {
            get => _selectedConference;
            set
            {
                if (_selectedConference == value) return;
                _selectedConference = value;
                OnPropertyChanged();
                ApplyFilter();
            }
        }

        private bool _showFollowedOnly;
        public bool ShowFollowedOnly
        {
            get => _showFollowedOnly;
            set
            {
                if (_showFollowedOnly == value) return;
                _showFollowedOnly = value;
                OnPropertyChanged();
                ApplyFilter();
            }
        }

        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            set { _isLoading = value; OnPropertyChanged(); }
        }

        private string _statusMessage = string.Empty;
        public string StatusMessage
        {
            get => _statusMessage;
            set { _statusMessage = value; OnPropertyChanged(); }
        }

        public ICommand ToggleFollowCommand { get; }

        private const string FollowedTeamsKey = "FollowedTeams";

        public TeamsViewModel(GameDataApiService apiService)
        {
            _apiService = apiService;
            ToggleFollowCommand = new Command<TeamInfo>(ToggleFollow);
        }

        public async Task LoadAsync()
        {
            IsLoading = true;
            StatusMessage = string.Empty;

            try
            {
                var teams = await _apiService.GetTeamsAsync();
                if (teams == null || teams.Count == 0)
                {
                    StatusMessage = "No teams available.";
                    return;
                }

                // Load persisted follow state
                var followed = GetFollowedIds();
                foreach (var t in teams)
                    t.IsFollowed = followed.Contains(t.TeamID);

                _allTeams = [.. teams.OrderBy(t => t.TeamName)];

                ConferenceFilters = new ObservableCollection<string>(ConferenceHelper.FilterDisplayList());
                ApplyFilter();
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error loading teams: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void ApplyFilter()
        {
            var filtered = _allTeams.AsEnumerable();

            if (SelectedConference != "All")
            {
                var abbr = ConferenceHelper.DisplayToAbbr(SelectedConference);
                filtered = filtered.Where(t => t.ConferenceAbbr == abbr);
            }

            if (ShowFollowedOnly)
                filtered = filtered.Where(t => t.IsFollowed);

            Teams = new ObservableCollection<TeamInfo>(filtered);
        }

        private void ToggleFollow(TeamInfo? team)
        {
            if (team == null) return;

            team.IsFollowed = !team.IsFollowed;

            var followed = GetFollowedIds();
            if (team.IsFollowed)
                followed.Add(team.TeamID);
            else
                followed.Remove(team.TeamID);

            Preferences.Default.Set(FollowedTeamsKey, string.Join(",", followed));

            // Only rebuild the list if followed-only mode is on (row needs to appear/disappear)
            if (ShowFollowedOnly)
                ApplyFilter();
        }

        public static HashSet<int> GetFollowedIds()
        {
            var raw = Preferences.Default.Get(FollowedTeamsKey, string.Empty);
            if (string.IsNullOrEmpty(raw)) return [];
            return raw.Split(',')
                      .Where(s => int.TryParse(s, out _))
                      .Select(int.Parse)
                      .ToHashSet();
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
