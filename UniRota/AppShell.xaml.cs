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

            StartupContent.Content = startupPage;
            LoginContent.Content = loginPage;
            RegisterContent.Content = registerPage;
            HomeContent.Content = homePage;
        }
    }
}
