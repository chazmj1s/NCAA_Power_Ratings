using NCAA_Power_Ratings.Mobile.ViewModels;

namespace NCAA_Power_Ratings.Mobile.Views
{
    public partial class ProjectionsPage : ContentPage
    {
        private readonly ProjectionsViewModel _viewModel;

        public ProjectionsPage(ProjectionsViewModel viewModel)
        {
            InitializeComponent();
            BindingContext = viewModel;
            _viewModel = viewModel;
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        }

        private void OnViewModelPropertyChanged(
            object sender,
            System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ProjectionsViewModel.SelectedWeek))
                ScrollToSelectedWeek();
        }

        private void ScrollToSelectedWeek()
        {
            var weeks    = _viewModel.Weeks;
            var selected = _viewModel.SelectedWeek;
            var index    = weeks.ToList().FindIndex(w => w.Week == selected);
            if (index < 0) return;

            const double itemWidth = 42.0;
            var scrollX = Math.Max(0, (index * itemWidth) - 150);

            MainThread.BeginInvokeOnMainThread(async () =>
            {
                await Task.Delay(100);
                await WeekScrollView.ScrollToAsync(scrollX, 0, animated: true);
            });
        }
    }
}
