using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using UniRota.Services.Interfaces;

namespace UniRota.ViewModels;

public partial class ReceivedRequestsViewModel : ObservableObject
{
    private readonly IRideRequestService _rideRequestService;

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private string errorMessage = string.Empty;

    [ObservableProperty]
    private bool hasError;

    [ObservableProperty]
    private string feedbackMessage = string.Empty;

    [ObservableProperty]
    private bool hasFeedback;

    [ObservableProperty]
    private bool isEmpty = true;

    public ReceivedRequestsViewModel(IRideRequestService rideRequestService)
    {
        _rideRequestService = rideRequestService;
    }

    public ObservableCollection<RideRequestItemViewModel> Requests { get; } = [];

    public bool IsNotBusy => !IsBusy;

    public bool ShowEmptyState =>
        IsEmpty && !HasError && !IsBusy;

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
        ClearMessages();
        Requests.Clear();
        IsEmpty = false;

        try
        {
            await RefreshRequestsAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            SetError(
                exception.Message,
                "Não foi possível carregar as solicitações recebidas. Tente novamente.");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand(AllowConcurrentExecutions = false)]
    private Task AcceptAsync(
        RideRequestItemViewModel? item,
        CancellationToken cancellationToken)
    {
        return ProcessAsync(
            item,
            accept: true,
            cancellationToken);
    }

    [RelayCommand(AllowConcurrentExecutions = false)]
    private Task RejectAsync(
        RideRequestItemViewModel? item,
        CancellationToken cancellationToken)
    {
        return ProcessAsync(
            item,
            accept: false,
            cancellationToken);
    }

    private async Task ProcessAsync(
        RideRequestItemViewModel? item,
        bool accept,
        CancellationToken cancellationToken)
    {
        if (item is null || IsBusy)
        {
            return;
        }

        IsBusy = true;
        ClearMessages();

        try
        {
            if (accept)
            {
                await _rideRequestService.AcceptAsync(
                    item.Request.Id,
                    cancellationToken);
            }
            else
            {
                await _rideRequestService.RejectAsync(
                    item.Request.Id,
                    cancellationToken);
            }

            Requests.Remove(item);
            IsEmpty = Requests.Count == 0;
            FeedbackMessage = accept
                ? "Solicitação aceita."
                : "Solicitação recusada.";
            HasFeedback = true;

            try
            {
                await RefreshRequestsAsync(cancellationToken);
            }
            catch (Exception exception)
                when (exception is not OperationCanceledException)
            {
                SetError(
                    exception.Message,
                    "A ação foi concluída, mas não foi possível atualizar a lista.");
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            SetError(
                exception.Message,
                accept
                    ? "Não foi possível aceitar a solicitação. Tente novamente."
                    : "Não foi possível recusar a solicitação. Tente novamente.");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task RefreshRequestsAsync(CancellationToken cancellationToken)
    {
        var requests = await _rideRequestService
            .GetReceivedPendingRequestsAsync(cancellationToken);

        Requests.Clear();

        foreach (var request in requests)
        {
            Requests.Add(new RideRequestItemViewModel(
                request,
                request.PassengerUserName,
                "Passageiro"));
        }

        IsEmpty = Requests.Count == 0;
    }

    private void ClearMessages()
    {
        ErrorMessage = string.Empty;
        HasError = false;
        FeedbackMessage = string.Empty;
        HasFeedback = false;
    }

    private void SetError(string message, string fallbackMessage)
    {
        ErrorMessage = string.IsNullOrWhiteSpace(message)
            ? fallbackMessage
            : message;
        HasError = true;
    }
}
