using NCAA_Power_Ratings.Mobile.ViewModels;

namespace NCAA_Power_Ratings.Mobile.Views
{
    public partial class SchedulePage : ContentPage
    {
        private ScheduleViewModel ViewModel => (ScheduleViewModel)BindingContext;
        private bool _pickersReady = false;

        public SchedulePage(ScheduleViewModel viewModel)
        {
            InitializeComponent();
            BindingContext = viewModel;

            var yearIndex = 2025 - 2021;
            if (yearIndex >= 0 && yearIndex < YearPicker.Items.Count)
                YearPicker.SelectedIndex = yearIndex;

            FilterPicker.SelectedIndex = 0;

            // TeamPicker is ItemsSource-bound; default to "All Teams" once names are loaded
            viewModel.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(ScheduleViewModel.TeamNames))
                {
                    var picker = this.FindByName<Picker>("TeamPicker");
                    if (picker != null && picker.SelectedIndex < 0)
                        picker.SelectedIndex = 0;
                }
            };

            _pickersReady = true;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            if (BindingContext is ScheduleViewModel vm)
                await vm.LoadDataAsync();
        }

        private void OnYearChanged(object sender, EventArgs e)
        {
            if (!_pickersReady) return;
            if (sender is Picker picker && picker.SelectedIndex >= 0)
                if (int.TryParse(picker.Items[picker.SelectedIndex], out int year))
                    ViewModel.SelectedYear = year;
        }

        private void OnFilterChanged(object sender, EventArgs e)
        {
            if (!_pickersReady) return;
            if (sender is not Picker picker || picker.SelectedIndex < 0) return;
            var selected = picker.Items[picker.SelectedIndex];
            if (selected.StartsWith("\u2500")) return;
            ViewModel.ApplyFilter(selected);
        }

        private void OnTeamFilterChanged(object sender, EventArgs e)
        {
            if (!_pickersReady) return;
            if (sender is Picker picker && picker.SelectedItem is string team)
                ViewModel.ApplyTeamFilter(team);
        }
    }
}
