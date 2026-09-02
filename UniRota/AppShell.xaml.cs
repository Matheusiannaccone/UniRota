namespace UniRota
{
    public partial class AppShell : Shell
    {
        public AppShell(
            Views.StartupPage startupPage,
            Views.Auth.LoginPage loginPage,
            Views.Auth.RegisterPage registerPage,
            Views.HomePage homePage)
        {
            InitializeComponent();

            Routing.RegisterRoute(
                nameof(Views.Routes.MyRoutesPage),
                typeof(Views.Routes.MyRoutesPage));
            Routing.RegisterRoute(
                nameof(Views.Routes.NewRoutePage),
                typeof(Views.Routes.NewRoutePage));
            Routing.RegisterRoute(
                nameof(Views.Matching.FindRidePage),
                typeof(Views.Matching.FindRidePage));
            Routing.RegisterRoute(
                nameof(Views.Matching.MatchResultsPage),
                typeof(Views.Matching.MatchResultsPage));
            Routing.RegisterRoute(
                nameof(Views.Matching.RideRequestPage),
                typeof(Views.Matching.RideRequestPage));

            StartupContent.Content = startupPage;
            LoginContent.Content = loginPage;
            RegisterContent.Content = registerPage;
            HomeContent.Content = homePage;
        }
    }
}
