using NCAA_Power_Ratings.Mobile.ViewModels;

namespace NCAA_Power_Ratings.Mobile.Views
{
    public partial class TeamsPage : ContentPage
    {
        private readonly TeamsViewModel _viewModel;

        public TeamsPage(TeamsViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            BindingContext = _viewModel;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            if (_viewModel.Teams.Count == 0)
                await _viewModel.LoadAsync();
        }
    }
}
