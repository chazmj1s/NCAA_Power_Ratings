using NCAA_Power_Ratings.Mobile.ViewModels;

namespace NCAA_Power_Ratings.Mobile.Views
{
    public partial class FollowingPage : ContentPage
    {
        public FollowingPage(FollowingViewModel viewModel)
        {
            InitializeComponent();
            BindingContext = viewModel;
        }
    }
}
