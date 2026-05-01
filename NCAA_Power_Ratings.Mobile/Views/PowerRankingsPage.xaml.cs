using NCAA_Power_Ratings.Mobile.ViewModels;
using Microsoft.Maui.Controls;

namespace NCAA_Power_Ratings.Mobile.Views
{
    public partial class PowerRankingsPage : ContentPage
    {
        private PowerRankingsViewModel ViewModel => (PowerRankingsViewModel)BindingContext;

        public PowerRankingsPage(PowerRankingsViewModel viewModel)
        {
            InitializeComponent();
            BindingContext = viewModel;

            // Set the initial year picker selection to 2025
            var yearIndex = 2025 - 2021; // 2021 is index 0
            if (yearIndex >= 0 && yearIndex < YearPicker.Items.Count)
            {
                YearPicker.SelectedIndex = yearIndex;
            }
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();

            // Load data when page appears
            if (BindingContext is PowerRankingsViewModel viewModel)
            {
                await viewModel.LoadDataAsync();
            }
        }

        private void OnYearChanged(object sender, EventArgs e)
        {
            if (sender is Picker picker && picker.SelectedIndex >= 0)
            {
                var yearString = picker.Items[picker.SelectedIndex];
                if (int.TryParse(yearString, out int year))
                {
                    ViewModel.SelectedYear = year;
                }
            }
        }
    }
}
