using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using UniRota.Services.Interfaces;

namespace UniRota.ViewModels;

public partial class ConfirmedRoutesViewModel : ObservableObject
{
    private readonly IRideRequestService _rideRequestService;
    private readonly IAuthService _authService;

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private string errorMessage = string.Empty;

    [ObservableProperty]
    private bool hasError;

    [ObservableProperty]
    private bool isEmpty = true;

    public ConfirmedRoutesViewModel(
        IRideRequestService rideRequestService,
        IAuthService authService)
    {
        _rideRequestService = rideRequestService;
        _authService = authService;
    }

    public ObservableCollection<ConfirmedRideItemViewModel> Routes { get; } = [];

    public bool IsNotBusy => !IsBusy;

    public bool ShowEmptyState => IsEmpty && !HasError && !IsBusy;

    partial void OnIsBusyChanged(bool value)
    {
        OnPropertyChanged(nameof(IsNotBusy));
        OnPropertyChanged(nameof(ShowEmptyState));
    }

    partial void OnHasErrorChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowEmptyState));
    }

    partial void OnIsEmptyChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowEmptyState));
    }

    [RelayCommand(AllowConcurrentExecutions = false)]
    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        ClearError();
        Routes.Clear();
        IsEmpty = false;

        try
        {
            var userId = _authService.CurrentUser?.Id
                ?? throw new InvalidOperationException(
                    "Não há uma sessão autenticada. Entre novamente para continuar.");
            var requests = await _rideRequestService
                .GetMyAcceptedRequestsAsync(cancellationToken);

            if (!string.Equals(
                    _authService.CurrentUser?.Id,
                    userId,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "A sessão autenticada foi alterada durante a operação. Tente novamente.");
            }

            foreach (var request in requests)
            {
                Routes.Add(new ConfirmedRideItemViewModel(
                    request,
                    userId));
            }

            IsEmpty = Routes.Count == 0;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            SetError(
                exception.Message,
                "Não foi possível carregar suas rotas confirmadas. Tente novamente.");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ClearError()
    {
        ErrorMessage = string.Empty;
        HasError = false;
    }

    private void SetError(string message, string fallbackMessage)
    {
        ErrorMessage = string.IsNullOrWhiteSpace(message)
            ? fallbackMessage
            : message;
        HasError = true;
    }
}
