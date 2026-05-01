using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using NCAA_Power_Ratings.Mobile.Models;
using NCAA_Power_Ratings.Mobile.Services;

namespace NCAA_Power_Ratings.Mobile.ViewModels
{
    /// <summary>
    /// ViewModel for the Power Rankings page with sorting and filtering
    /// </summary>
    public class PowerRankingsViewModel : INotifyPropertyChanged
    {
        private readonly GameDataApiService _apiService;
        private List<TeamRanking> _allTeams = new();
        private ObservableCollection<TeamRanking> _filteredTeams = new();
        private bool _isBusy;
        private string _selectedConference = "All";
        private RankingFilter _currentFilter = RankingFilter.All;
        private RankingSort _currentSort = RankingSort.Rank;
        private bool _isSortAscending = true;
        private int _selectedYear;

        public PowerRankingsViewModel(GameDataApiService apiService)
        {
            _apiService = apiService;
            _selectedYear = 2025; // Default to most recent year with data

            // Initialize commands - use Microsoft.Maui.Controls.Command
            LoadDataCommand = new Microsoft.Maui.Controls.Command(async () => await LoadDataAsync());
            ApplyFilterCommand = new Microsoft.Maui.Controls.Command<string>(ApplyFilter);
            ApplySortCommand = new Microsoft.Maui.Controls.Command<RankingSort>(ApplySort);
            SortColumnCommand = new Microsoft.Maui.Controls.Command<string>(SortByColumn);
            RefreshCommand = new Microsoft.Maui.Controls.Command(async () => await RefreshDataAsync());

            // Available conferences for filtering
            Conferences = new ObservableCollection<string>
            {
                "All", "SEC", "Big Ten", "Big 12", "ACC", "Pac-12", "AAC", "Mountain West",
                "Sun Belt", "MAC", "Conference USA", "Independent"
            };
        }

        #region Properties

        public ObservableCollection<TeamRanking> FilteredTeams
        {
            get => _filteredTeams;
            set { _filteredTeams = value; OnPropertyChanged(); }
        }

        public ObservableCollection<string> Conferences { get; }

        public bool IsBusy
        {
            get => _isBusy;
            set { _isBusy = value; OnPropertyChanged(); }
        }

        public string SelectedConference
        {
            get => _selectedConference;
            set
            {
                _selectedConference = value;
                OnPropertyChanged();
                ApplyFilter("Conference");
            }
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
                    // Reload data when year changes
                    _ = LoadDataAsync();
                }
            }
        }

        public string StatusMessage { get; private set; } = "Loading...";

        #endregion

        #region Commands

        public ICommand LoadDataCommand { get; }
        public ICommand ApplyFilterCommand { get; }
        public ICommand ApplySortCommand { get; }
        public ICommand SortColumnCommand { get; }
        public ICommand RefreshCommand { get; }

        #endregion

        #region Methods

        public async Task LoadDataAsync()
        {
            if (IsBusy) return;

            IsBusy = true;
            StatusMessage = "Loading rankings...";
            OnPropertyChanged(nameof(StatusMessage));

            try
            {
                System.Diagnostics.Debug.WriteLine($"[PowerRankings] Starting to load data for year {SelectedYear}");

                var teams = await _apiService.GetPowerRankingsAsync(SelectedYear);

                if (teams != null)
                {
                    System.Diagnostics.Debug.WriteLine($"[PowerRankings] Loaded {teams.Count} teams");
                    _allTeams = teams;
                    ApplyFiltersAndSort();
                    StatusMessage = $"Loaded {teams.Count} teams";
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("[PowerRankings] No teams returned from API");
                    StatusMessage = "Failed to load rankings";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error: {ex.Message}";
                System.Diagnostics.Debug.WriteLine($"[PowerRankings] Error loading data: {ex}");
            }
            finally
            {
                IsBusy = false;
                OnPropertyChanged(nameof(StatusMessage));
            }
        }

        private async Task RefreshDataAsync()
        {
            await LoadDataAsync();
        }

        public void ApplyFilter(string filterType)
        {
            switch (filterType)
            {
                case "All":
                    _currentFilter = RankingFilter.All;
                    break;
                case "Top25":
                    _currentFilter = RankingFilter.Top25;
                    break;
                case "Conference":
                    _currentFilter = RankingFilter.Conference;
                    break;
            }

            ApplyFiltersAndSort();
        }

        public void ApplySort(RankingSort sortType)
        {
            _currentSort = sortType;
            ApplyFiltersAndSort();
        }

        /// <summary>
        /// Handles column header clicks - toggles sort direction if same column, otherwise sets new column
        /// </summary>
        public void SortByColumn(string columnName)
        {
            var newSort = columnName switch
            {
                "Rank" => RankingSort.Rank,
                "Team" => RankingSort.TeamName,
                "Record" => RankingSort.Record,
                "Rating" => RankingSort.PowerRating,
                "Conference" => RankingSort.Conference,
                "SOS" => RankingSort.SOS,
                _ => RankingSort.Rank
            };

            // If clicking the same column, toggle direction
            if (_currentSort == newSort)
            {
                _isSortAscending = !_isSortAscending;
            }
            else
            {
                // New column, default to ascending (except for rating/SOS which default to descending)
                _currentSort = newSort;
                _isSortAscending = newSort switch
                {
                    RankingSort.PowerRating => false,
                    RankingSort.SOS => false,
                    _ => true
                };
            }

            ApplyFiltersAndSort();
        }

        private void ApplyFiltersAndSort()
        {
            // Start with all teams
            var filtered = _allTeams.AsEnumerable();

            // Apply filters
            filtered = _currentFilter switch
            {
                RankingFilter.Top25 => filtered.Where(t => t.IsTop25),
                RankingFilter.Conference when SelectedConference != "All" =>
                    filtered.Where(t => t.ConferenceAbbr == SelectedConference),
                RankingFilter.P4 => filtered.Where(t => t.Tier == "P4"),
                RankingFilter.G5 => filtered.Where(t => t.Tier == "G5"),
                RankingFilter.Independent => filtered.Where(t => t.Tier == "Independent"),
                _ => filtered
            };

            // Apply sorting with direction
            IOrderedEnumerable<TeamRanking> sorted = _currentSort switch
            {
                RankingSort.Rank => _isSortAscending 
                    ? filtered.OrderBy(t => t.Rank)
                    : filtered.OrderByDescending(t => t.Rank),
                RankingSort.TeamName => _isSortAscending
                    ? filtered.OrderBy(t => t.TeamName)
                    : filtered.OrderByDescending(t => t.TeamName),
                RankingSort.PowerRating => _isSortAscending
                    ? filtered.OrderBy(t => t.Ranking ?? 0)
                    : filtered.OrderByDescending(t => t.Ranking ?? 0),
                RankingSort.Record => _isSortAscending
                    ? filtered.OrderBy(t => t.Wins).ThenBy(t => t.Losses)
                    : filtered.OrderByDescending(t => t.Wins).ThenBy(t => t.Losses),
                RankingSort.Conference => _isSortAscending
                    ? filtered.OrderBy(t => t.Conference).ThenBy(t => t.Rank)
                    : filtered.OrderByDescending(t => t.Conference).ThenBy(t => t.Rank),
                RankingSort.SOS => _isSortAscending
                    ? filtered.OrderBy(t => t.CombinedSOS ?? 0)
                    : filtered.OrderByDescending(t => t.CombinedSOS ?? 0),
                RankingSort.TierRank => _isSortAscending
                    ? filtered.OrderBy(t => t.TierRank)
                    : filtered.OrderByDescending(t => t.TierRank),
                RankingSort.Tier => _isSortAscending
                    ? filtered.OrderBy(t => t.Tier).ThenBy(t => t.TierRank)
                    : filtered.OrderByDescending(t => t.Tier).ThenBy(t => t.TierRank),
                _ => filtered.OrderBy(t => t.Rank)
            };

            FilteredTeams = new ObservableCollection<TeamRanking>(sorted);
        }

        #endregion

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion
    }
}
