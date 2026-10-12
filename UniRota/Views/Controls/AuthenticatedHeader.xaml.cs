using System.Windows.Input;

namespace UniRota.Views.Controls;

public partial class AuthenticatedHeader : ContentView
{
    public static readonly BindableProperty IsHomeProperty =
        BindableProperty.Create(nameof(IsHome), typeof(bool), typeof(AuthenticatedHeader), false);

    public static readonly BindableProperty LogoutCommandProperty =
        BindableProperty.Create(nameof(LogoutCommand), typeof(ICommand), typeof(AuthenticatedHeader));

    public static readonly BindableProperty LogoutIsEnabledProperty =
        BindableProperty.Create(nameof(LogoutIsEnabled), typeof(bool), typeof(AuthenticatedHeader), true);

    private Shell? _shell;
    private bool _isNavigating;

    public bool IsHome
    {
        get => (bool)GetValue(IsHomeProperty);
        set => SetValue(IsHomeProperty, value);
    }

    public ICommand? LogoutCommand
    {
        get => (ICommand?)GetValue(LogoutCommandProperty);
        set => SetValue(LogoutCommandProperty, value);
    }

    public bool LogoutIsEnabled
    {
        get => (bool)GetValue(LogoutIsEnabledProperty);
        set => SetValue(LogoutIsEnabledProperty, value);
    }

    public AuthenticatedHeader()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object? sender, EventArgs e)
    {
        if (_shell is not null)
            _shell.Navigated -= OnShellNavigated;

        _shell = Shell.Current;
        if (_shell is not null)
            _shell.Navigated += OnShellNavigated;

        UpdateNavigationActions();
    }

    private void OnUnloaded(object? sender, EventArgs e)
    {
        if (_shell is not null)
            _shell.Navigated -= OnShellNavigated;

        _shell = null;
    }

    private void OnShellNavigated(object? sender, ShellNavigatedEventArgs e) => UpdateNavigationActions();

    private void UpdateNavigationActions()
    {
        HomeButton.IsEnabled = !_isNavigating && _shell is not null;
        // Nas raízes das abas não há página anterior no stack do Shell.
        BackButton.IsEnabled = !_isNavigating && _shell?.Navigation.NavigationStack.Count > 1;
    }

    private async void OnHomeClicked(object? sender, EventArgs e) => await NavigateAsync(AppShell.HomeRoute);

    private async void OnBackClicked(object? sender, EventArgs e)
    {
        if (_shell?.Navigation.NavigationStack.Count > 1)
            await NavigateAsync("..");
    }

    private async Task NavigateAsync(string route)
    {
        if (_isNavigating || _shell is null)
            return;

        var shell = _shell;
        _isNavigating = true;
        UpdateNavigationActions();
        try
        {
            await shell.GoToAsync(route);
        }
        catch (Exception)
        {
            await shell.DisplayAlert("Navegação", "Não foi possível abrir a tela. Tente novamente.", "OK");
        }
        finally
        {
            _isNavigating = false;
            UpdateNavigationActions();
        }
    }
}
