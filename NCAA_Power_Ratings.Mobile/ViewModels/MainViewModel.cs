using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace NCAA_Power_Ratings.Mobile.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private int _selectedIndex = 0;

        public MainViewModel()
        {
            SelectTabCommand = new Microsoft.Maui.Controls.Command<int>(idx =>
            {
                SelectedIndex = idx;
            });

            NextTabCommand = new Microsoft.Maui.Controls.Command(() =>
            {
                if (SelectedIndex < TabItems.Count - 1)
                    SelectedIndex++;
            });

            PreviousTabCommand = new Microsoft.Maui.Controls.Command(() =>
            {
                if (SelectedIndex > 0)
                    SelectedIndex--;
            });
        }

        public ObservableCollection<TabItem> TabItems { get; } = new();
        public ObservableCollection<object>  Pages    { get; } = new();

        public int SelectedIndex
        {
            get => _selectedIndex;
            set
            {
                if (_selectedIndex != value)
                {
                    _selectedIndex = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(ScoresSelected));
                }
            }
        }

        // Scores tab active — week bar on SchedulePage binds to this if needed
        public bool ScoresSelected => _selectedIndex == 0;

        public ICommand SelectTabCommand   { get; }
        public ICommand NextTabCommand     { get; }
        public ICommand PreviousTabCommand { get; }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    // ── Tab item with IsSelected for underline binding ────────────────

    public class TabItem : INotifyPropertyChanged
    {
        private bool _isSelected;

        public string Label { get; init; } = string.Empty;
        public int    Index { get; init; }

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
