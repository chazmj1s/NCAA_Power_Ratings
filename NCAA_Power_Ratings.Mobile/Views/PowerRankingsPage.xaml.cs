using NCAA_Power_Ratings.Mobile.ViewModels;
using Microsoft.Maui.Controls;

namespace NCAA_Power_Ratings.Mobile.Views
{
    public partial class PowerRankingsPage : ContentPage
    {
        private PowerRankingsViewModel ViewModel => (PowerRankingsViewModel)BindingContext;
        private bool _pickersReady = false;

        public PowerRankingsPage(PowerRankingsViewModel viewModel)
        {
            InitializeComponent();
            BindingContext = viewModel;

            var yearIndex = 2025 - 2021;
            if (yearIndex >= 0 && yearIndex < YearPicker.Items.Count)
                YearPicker.SelectedIndex = yearIndex;

            FilterPicker.SelectedIndex = 0;
            _pickersReady = true;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            if (BindingContext is PowerRankingsViewModel viewModel)
                await viewModel.LoadDataAsync();
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

            // Ignore separator rows
            if (selected.StartsWith("\u2500")) return;

            var filterParam = selected switch
            {
                "Top 25"     => "Top25",
                _            => selected   // All, P4, G5, conference abbrs, Independent pass through
            };

            ViewModel.ApplyFilter(filterParam);
        }
    }
}
