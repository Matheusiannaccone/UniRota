using UniRota.Services.Interfaces;

namespace UniRota;

public partial class AppShell : Shell
{
    public const string HomeRoute = "//main/home/HomePage";
    private readonly IServiceProvider _services;
    private readonly IAuthService _authService;

    public AppShell(
        Views.StartupPage startupPage,
        Views.Auth.LoginPage loginPage,
        Views.Auth.RegisterPage registerPage,
        IServiceProvider services,
        IAuthService authService)
    {
        _services = services;
        _authService = authService;
        InitializeComponent();

        Routing.RegisterRoute(nameof(Views.Routes.NewRoutePage), typeof(Views.Routes.NewRoutePage));
        Routing.RegisterRoute(nameof(Views.Matching.FindRidePage), typeof(Views.Matching.FindRidePage));
        Routing.RegisterRoute(nameof(Views.Matching.MatchResultsPage), typeof(Views.Matching.MatchResultsPage));
        Routing.RegisterRoute(nameof(Views.Matching.RideRequestPage), typeof(Views.Matching.RideRequestPage));
        Routing.RegisterRoute(nameof(Views.Matching.RouteDetailsPage), typeof(Views.Matching.RouteDetailsPage));
        Routing.RegisterRoute(nameof(Views.Matching.AwaitingApprovalPage), typeof(Views.Matching.AwaitingApprovalPage));
        Routing.RegisterRoute(nameof(Views.Matching.ReceivedRequestsPage), typeof(Views.Matching.ReceivedRequestsPage));

        StartupContent.Content = startupPage;
        LoginContent.Content = loginPage;
        RegisterContent.Content = registerPage;
        CreateAuthenticatedTabs();
    }

    private void CreateAuthenticatedTabs()
    {
        MainTabs.Items.Clear();
        AddTab<Views.HomePage>("Início", "home", "tab_home.png");
        AddTab<Views.Routes.MyRoutesPage>("Rotas", "routes", "tab_routes.png");
        AddTab<Views.Matching.ConfirmedRoutesPage>("Caronas", "rides", "tab_rides.png");
    }

    private void AddTab<TPage>(string title, string route, string icon) where TPage : ContentPage
    {
        var tab = new Tab { Title = title, Route = route, Icon = icon };
        tab.Items.Add(new ShellContent
        {
            Title = title,
            Route = typeof(TPage).Name,
            ContentTemplate = new DataTemplate(() => _services.GetRequiredService<TPage>())
        });
        MainTabs.Items.Add(tab);
    }

    public void SelectMainTab(string route)
    {
        if (_authService.CurrentUser is null)
            return;

        // Trocar a aba conserva seu stack; não empilha outra página raiz.
        MainTabs.CurrentItem = MainTabs.Items.Single(tab => tab.Route == route);
        CurrentItem = MainTabs;
    }

    protected override void OnNavigating(ShellNavigatingEventArgs args)
    {
        if (_authService is not null && _authService.CurrentUser is null
            && args.Target.Location.OriginalString.Contains("//main", StringComparison.Ordinal)
            && args.CanCancel)
        {
            args.Cancel();
            return;
        }

        base.OnNavigating(args);
    }

    protected override void OnNavigated(ShellNavigatedEventArgs args)
    {
        base.OnNavigated(args);
        if (_authService is not null && _authService.CurrentUser is null
            && args.Current.Location.OriginalString.EndsWith("/login", StringComparison.Ordinal))
        {
            // Novas instâncias e stacks após logout: nenhum dado da sessão anterior.
            CreateAuthenticatedTabs();
        }
    }
}
