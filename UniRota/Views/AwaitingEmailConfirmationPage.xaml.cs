using UniRota.ViewModels;

namespace UniRota.Views;

public partial class AwaitingEmailConfirmationPage : ContentPage
{
    private readonly AwaitingEmailConfirmationViewModel _viewModel;
    private IDispatcherTimer? _timer;
    private Window? _observedWindow;

    public AwaitingEmailConfirmationPage(AwaitingEmailConfirmationViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        ObserveWindow();
        _timer ??= Dispatcher.CreateTimer();
        _timer.Interval = TimeSpan.FromSeconds(1);
        _timer.Tick += OnTimerTick;
        _timer.Start();
        await _viewModel.CheckAutomaticallyAsync();
    }

    private void OnTimerTick(object? sender, EventArgs e) => _viewModel.RefreshState();
    private void OnLoaded(object? sender, EventArgs e) => ObserveWindow();
    private void OnUnloaded(object? sender, EventArgs e) => StopObservingWindow();

    private void ObserveWindow()
    {
        // No Shell, OnAppearing pode ocorrer antes de a página estar ligada à Window.
        if (ReferenceEquals(_observedWindow, Window)) return;
        StopObservingWindow();
        _observedWindow = Window;
        if (_observedWindow is not null)
        {
            // Activated corresponde a OnResume no Android, inclusive retorno após OnPause.
            _observedWindow.Activated += OnActivated;
            _observedWindow.Deactivated += OnDeactivated;
        }
    }

    private async void OnActivated(object? sender, EventArgs e)
    {
        if (Shell.Current?.CurrentPage == this)
            await _viewModel.CheckAutomaticallyAsync();
    }

    private void OnDeactivated(object? sender, EventArgs e) => _viewModel.RecoveryPassword = string.Empty;

    private void StopObservingWindow()
    {
        if (_observedWindow is not null)
        {
            _observedWindow.Activated -= OnActivated;
            _observedWindow.Deactivated -= OnDeactivated;
        }
        _observedWindow = null;
    }

    protected override void OnDisappearing()
    {
        // A observação termina em Unloaded; ir para segundo plano não deve removê-la.
        if (_timer is not null) { _timer.Stop(); _timer.Tick -= OnTimerTick; }
        _viewModel.RecoveryPassword = string.Empty;
        base.OnDisappearing();
    }

    protected override bool OnBackButtonPressed() => true;
}
