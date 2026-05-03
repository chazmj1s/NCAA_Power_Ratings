using NCAA_Power_Ratings.Mobile.ViewModels;

namespace NCAA_Power_Ratings.Mobile.Views
{
    public partial class MainPage : ContentPage
    {
        private readonly MainViewModel     _vm;
        private readonly SchedulePage      _schedulePage;
        private readonly PowerRankingsPage _rankingsPage;
        private readonly TeamsPage         _teamsPage;
        private readonly RivalriesPage     _rivalriesPage;
        private readonly ConfigPage        _configPage;

        public MainPage(
            MainViewModel mainViewModel,
            SchedulePage schedulePage,
            PowerRankingsPage rankingsPage,
            TeamsPage teamsPage,
            RivalriesPage rivalriesPage,
            ConfigPage configPage)
        {
            InitializeComponent();

            _vm            = mainViewModel;
            _schedulePage  = schedulePage;
            _rankingsPage  = rankingsPage;
            _teamsPage     = teamsPage;
            _rivalriesPage = rivalriesPage;
            _configPage    = configPage;

            BindingContext = _vm;

            // Build tab items
            _vm.TabItems.Clear();
            var labels = new[] { "Scores", "Rankings", "Teams", "Rivalries", "Config" };
            for (int i = 0; i < labels.Length; i++)
                _vm.TabItems.Add(new TabItem { Label = labels[i], Index = i, IsSelected = i == 0 });

            // Add all page views to the host grid, stacked on top of each other
            // Each page's Content (the root View) is added directly so BindingContext is preserved
            PageHost.Add(new ContentView { Content = _schedulePage.Content, BindingContext = _schedulePage.BindingContext });
            PageHost.Add(new ContentView { Content = _rankingsPage.Content, BindingContext = _rankingsPage.BindingContext });
            PageHost.Add(new ContentView { Content = _teamsPage.Content, BindingContext = _teamsPage.BindingContext });
            PageHost.Add(new ContentView { Content = _rivalriesPage.Content, BindingContext = _rivalriesPage.BindingContext });
            PageHost.Add(new ContentView { Content = _configPage.Content, BindingContext = _configPage.BindingContext });


            // Sync tab underline + page visibility when index changes
            _vm.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(MainViewModel.SelectedIndex))
                {
                    SyncTabItems(_vm.SelectedIndex);
                    SyncPage(_vm.SelectedIndex);
                }
            };

            // Show initial page
            SyncPage(0);

            // Trigger initial data load
            if (_schedulePage.BindingContext is ScheduleViewModel svm)
                _ = svm.LoadDataAsync();
        }

        private void SyncTabItems(int index)
        {
            foreach (var tab in _vm.TabItems)
                tab.IsSelected = tab.Index == index;
        }

        private void SyncPage(int index)
        {
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                // Toggle visibility
                for (int i = 0; i < PageHost.Count; i++)
                    if (PageHost.Children[i] is VisualElement ve)
                        ve.IsVisible = i == index;

                // Lazy load on first visit
                await Task.Delay(100);
                switch (index)
                {
                    case 0 when _schedulePage.BindingContext is ScheduleViewModel svm && !svm.HasLoaded:
                        await svm.LoadDataAsync(); break;
                    case 1 when _rankingsPage.BindingContext is PowerRankingsViewModel rvm && !rvm.HasLoaded:
                        await rvm.LoadDataAsync(); break;
                    case 2 when _teamsPage.BindingContext is TeamsViewModel tvm && !tvm.HasLoaded:
                        await tvm.LoadDataAsync(); break;
                    case 3 when _rivalriesPage.BindingContext is RivalriesViewModel riv && !riv.HasLoaded:
                        await riv.LoadDataAsync(); break;
                }
            });
        }
    }
}
