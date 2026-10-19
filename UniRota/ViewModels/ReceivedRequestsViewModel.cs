using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using UniRota.Models;
using UniRota.Services;
using UniRota.Services.Interfaces;

namespace UniRota.ViewModels;

public partial class ReceivedRequestsViewModel : ObservableObject
{
    private readonly IRideRequestService _rideRequestService;
    private readonly IRouteService _routeService;
    private readonly IAppClock _clock;

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

    public ReceivedRequestsViewModel(
        IRideRequestService rideRequestService,
        IRouteService routeService,
        IAppClock clock)
    {
        _rideRequestService = rideRequestService;
        _routeService = routeService;
        _clock = clock;
    }

    public ObservableCollection<RideRequestItemViewModel> Requests { get; } = [];

    public bool IsNotBusy => !IsBusy;

    public bool ShowEmptyState =>
        IsEmpty && !HasError && !IsBusy;

    partial void OnIsBusyChanged(bool value)
    {
        AcceptCommand.NotifyCanExecuteChanged();
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

    private bool CanAcceptRequest(RideRequestItemViewModel? item) =>
        !IsBusy && item?.CanAccept == true;

    [RelayCommand(AllowConcurrentExecutions = false, CanExecute = nameof(CanAcceptRequest))]
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
        if (item is null || IsBusy || item.Request.Status != RideRequestStatus.Pending
            || (accept && !item.CanAccept))
        {
            return;
        }

        if (accept && item.PassengerConflictMessage is not null)
        {
            SetError(item.PassengerConflictMessage, string.Empty);
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
                    cancellationToken,
                    confirmedDays: item.AcceptableDays);
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
            if (accept)
            {
                try
                {
                    await RefreshRequestsAsync(cancellationToken);
                }
                catch (Exception)
                {
                    Requests.Clear();
                    IsEmpty = true;
                }
            }

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
        var routes = requests.Count == 0
            ? new Dictionary<string, WeeklyRoute>()
            : (await _routeService.GetMyRoutesAsync(cancellationToken))
                .ToDictionary(route => route.Id);
        var passengerRoutes = await _rideRequestService.GetPassengerRoutesAsync(requests, cancellationToken);
        var today = _clock.Today;

        Requests.Clear();

        foreach (var request in requests)
        {
            IReadOnlyList<DayOfWeek> acceptableDays = [];
            string? passengerConflict = null;
            var canAccept = routes.TryGetValue(request.DriverRouteId, out var route)
                && RideRequestRules.CanAccept(request, route, today);
            if (request.Type == RideRequestType.Weekly)
            {
                canAccept = false;
                if (passengerRoutes.TryGetValue(request.PassengerRouteId, out var passenger))
                {
                    var freeDays = RideRequestRules.GetPassengerAvailableWeeklyDays(passenger, request.CompatibleDays, today);
                    if (freeDays.Count == 0) passengerConflict = RideRequestRules.PassengerDaysUnavailableMessage;
                    if (route is not null)
                    {
                        acceptableDays = RideRequestRules.GetAcceptableWeeklyDays(route.WeeklyAvailableSeats,
                            route.OneTimeReservations, freeDays.Where(route.DaysOfWeek.Contains).ToArray(), today);
                        canAccept = route.Role == RouteRole.Driver && route.UserId == request.DriverUserId
                            && acceptableDays.Count > 0;
                    }
                }
            }
            else if (passengerRoutes.TryGetValue(request.PassengerRouteId, out var passenger)
                && request.RequestedDate is { } date && date >= today)
            {
                passengerConflict = RideRequestRules.GetPassengerOnceConflict(passenger, date, today);
            }
            Requests.Add(new RideRequestItemViewModel(
                request,
                request.PassengerUserName,
                "Passageiro",
                canAccept || passengerConflict is not null, acceptableDays,
                passengerConflictMessage: passengerConflict));
        }

        IsEmpty = Requests.Count == 0;
    }

    public async Task ConfirmAndAcceptAsync(RideRequestItemViewModel item, Func<string, Task<bool>> confirm)
    {
        if (!CanAcceptRequest(item)) return;
        if (item.PassengerConflictMessage is not null)
        {
            SetError(item.PassengerConflictMessage, string.Empty);
            return;
        }
        if (await confirm(item.AcceptanceConfirmationMessage)) await AcceptCommand.ExecuteAsync(item);
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
