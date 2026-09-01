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

            StartupContent.Content = startupPage;
            LoginContent.Content = loginPage;
            RegisterContent.Content = registerPage;
            HomeContent.Content = homePage;
        }
    }
}
