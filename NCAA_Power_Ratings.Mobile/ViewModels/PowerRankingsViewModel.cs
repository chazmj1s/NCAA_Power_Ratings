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
        private RankingSort _currentSort = RankingSort.PowerRating;
        private bool _isSortAscending = false; // PowerRating defaults descending (highest = #1)
        private int _selectedYear;
        private string _selectedConferenceFilter = "All";

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

        /// <summary>Header label for the dynamic sort column shown in the list.</summary>
        public string ActiveSortLabel => _currentSort switch
        {
            RankingSort.PowerRating => "Rating",
            RankingSort.SOS        => "SOS",
            RankingSort.Record     => "Record",
            RankingSort.TierRank   => "Tier",
            RankingSort.Rank       => "Rank",
            _                      => "Rating"
        };

        /// <summary>Returns the display value for the active sort column for a given team.</summary>
        public string GetActiveSortValue(TeamRanking t) => _currentSort switch
        {
            RankingSort.PowerRating => t.DisplayRanking,
            RankingSort.SOS        => t.DisplaySOS,
            RankingSort.Record     => t.Record,
            RankingSort.TierRank   => t.DisplayTierWithRank,
            RankingSort.Rank       => t.DisplayRank,
            _                      => t.DisplayRanking
        };

        public string SelectedConferenceFilter
        {
            get => _selectedConferenceFilter;
            set
            {
                _selectedConferenceFilter = value;
                OnPropertyChanged();
            }
        }

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
                    _selectedConferenceFilter = "All";
                    break;
                case "Top25":
                    _currentFilter = RankingFilter.Top25;
                    break;
                case "Conference":
                    _currentFilter = RankingFilter.Conference;
                    break;
                case "P4":
                    _currentFilter = RankingFilter.P4;
                    break;
                case "G5":
                    _currentFilter = RankingFilter.G5;
                    break;
                case "Independent":
                    _currentFilter = RankingFilter.Independent;
                    break;
                default:
                    // Treat unknown values as a specific conference name
                    _currentFilter = RankingFilter.Conference;
                    _selectedConferenceFilter = filterType;
                    break;
            }

            ApplyFiltersAndSort();
        }

        public void ApplySort(RankingSort sortType)
        {
            _currentSort = sortType;
            OnPropertyChanged(nameof(ActiveSortLabel));
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
                "TierRank" => RankingSort.TierRank,
                "Tier" => RankingSort.Tier,
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
            OnPropertyChanged(nameof(ActiveSortLabel));
        }

        private void ApplyFiltersAndSort()
        {
            // Start with all teams
            var filtered = _allTeams.AsEnumerable();

            // Apply filters
            filtered = _currentFilter switch
            {
                RankingFilter.Top25 => filtered.Where(t => t.IsTop25),
                RankingFilter.Conference when _selectedConferenceFilter != "All" =>
                    filtered.Where(t =>
                        (t.ConferenceAbbr != null &&
                            t.ConferenceAbbr.Equals(_selectedConferenceFilter, StringComparison.OrdinalIgnoreCase)) ||
                        (t.Conference != null &&
                            t.Conference.Equals(_selectedConferenceFilter, StringComparison.OrdinalIgnoreCase))),
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

            var result = sorted.ToList();
            foreach (var team in result)
                team.ActiveSortValue = GetActiveSortValue(team);

            FilteredTeams = new ObservableCollection<TeamRanking>(result);
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
