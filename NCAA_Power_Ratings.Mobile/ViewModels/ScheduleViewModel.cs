using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using NCAA_Power_Ratings.Mobile.Models;
using NCAA_Power_Ratings.Mobile.Services;

namespace NCAA_Power_Ratings.Mobile.ViewModels
{
    public class ScheduleViewModel : INotifyPropertyChanged
    {
        private readonly GameDataApiService _apiService;
        private List<GameResult> _allGames = new();
        private ObservableCollection<GameResult> _filteredGames = new();
        private bool _isBusy;
        private int _selectedYear = 2025;
        private string _activeFilter = "All";
        private string _statusMessage = "Loading...";

        public ScheduleViewModel(GameDataApiService apiService)
        {
            _apiService = apiService;
            LoadDataCommand = new Microsoft.Maui.Controls.Command(async () => await LoadDataAsync());
            RefreshCommand  = new Microsoft.Maui.Controls.Command(async () => await LoadDataAsync());
        }

        public ObservableCollection<GameResult> FilteredGames
        {
            get => _filteredGames;
            set { _filteredGames = value; OnPropertyChanged(); }
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

        public ICommand LoadDataCommand { get; }
        public ICommand RefreshCommand { get; }

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
                    _allGames = games;
                    ApplyFilter(_activeFilter);
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

            IEnumerable<GameResult> filtered = _allGames;

            filtered = filterType switch
            {
                "Top25" => filtered.Where(g =>
                    // Can't determine Top 25 from game data alone — show all
                    true),
                "P4"    => filtered.Where(g => g.WinnerTier == "P4" || g.LoserTier == "P4"),
                "G5"    => filtered.Where(g => g.WinnerTier == "G5" || g.LoserTier == "G5"),
                "All"   => filtered,
                _       => filtered.Where(g =>
                    g.WinnerConf.Equals(filterType, StringComparison.OrdinalIgnoreCase) ||
                    g.LoserConf.Equals(filterType, StringComparison.OrdinalIgnoreCase))
            };

            FilteredGames = new ObservableCollection<GameResult>(filtered);
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
