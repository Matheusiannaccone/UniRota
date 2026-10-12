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
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        _observedWindow = Window;
        if (_observedWindow is not null) _observedWindow.Resumed += OnResumed;
        _timer ??= Dispatcher.CreateTimer();
        _timer.Interval = TimeSpan.FromSeconds(1);
        _timer.Tick += OnTimerTick;
        _timer.Start();
        await _viewModel.CheckAutomaticallyAsync();
    }

    private void OnTimerTick(object? sender, EventArgs e) => _viewModel.RefreshState();
    private async void OnResumed(object? sender, EventArgs e) => await _viewModel.CheckAutomaticallyAsync();

    protected override void OnDisappearing()
    {
        if (_observedWindow is not null) _observedWindow.Resumed -= OnResumed;
        _observedWindow = null;
        if (_timer is not null) { _timer.Stop(); _timer.Tick -= OnTimerTick; }
        _viewModel.RecoveryPassword = string.Empty;
        base.OnDisappearing();
    }

    protected override bool OnBackButtonPressed() => true;
}
