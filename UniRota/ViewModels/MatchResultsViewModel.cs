using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using UniRota.Models;
using UniRota.Services.Interfaces;

namespace UniRota.ViewModels;

public partial class MatchResultsViewModel : ObservableObject
{
    private readonly IRouteService _routeService;
    private readonly IMatchingService _matchingService;
    private readonly IRideRequestService _rideRequestService;
    private WeeklyRoute? _passengerRoute;

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private string errorMessage = string.Empty;

    [ObservableProperty]
    private bool hasError;

    [ObservableProperty]
    private bool isEmpty = true;

    [ObservableProperty]
    private string passengerRouteText = string.Empty;

    [ObservableProperty]
    private string passengerRouteDetailsText = string.Empty;

    public MatchResultsViewModel(
        IRouteService routeService,
        IMatchingService matchingService,
        IRideRequestService rideRequestService)
    {
        _routeService = routeService;
        _matchingService = matchingService;
        _rideRequestService = rideRequestService;
    }

    public event Action<WeeklyRoute, MatchResultItemViewModel>?
        RideRequestRequested;

    public event Action<WeeklyRoute, MatchResultItemViewModel>?
        RouteDetailsRequested;

    public ObservableCollection<MatchResultItemViewModel> Results { get; } = [];

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

    public void SetPassengerRoute(WeeklyRoute? passengerRoute)
    {
        _passengerRoute = passengerRoute;
        Results.Clear();
        IsEmpty = true;
        ClearError();

        PassengerRouteText = passengerRoute is null
            ? string.Empty
            : RoutePresentationText.GetOriginDestinationText(passengerRoute);
        PassengerRouteDetailsText = passengerRoute is null
            ? string.Empty
            : $"{RoutePresentationText.GetDaysText(passengerRoute.DaysOfWeek)} · "
              + RoutePresentationText.GetDepartureTimeText(
                  passengerRoute.DepartureTimeMinutes);
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
        Results.Clear();
        IsEmpty = false;

        try
        {
            if (_passengerRoute is null)
            {
                SetError(
                    "Não foi possível identificar a rota de passageiro selecionada.",
                    "Selecione novamente uma rota de passageiro.");
                return;
            }

            var driverRoutes = await _routeService.GetDriverRoutesAsync(
                cancellationToken);
            var activeRequests = await _rideRequestService
                .GetMyActiveRequestsAsync(cancellationToken);
            var unavailableDriverRouteIds = activeRequests
                .Where(request => string.Equals(
                    request.PassengerRouteId,
                    _passengerRoute.Id,
                    StringComparison.Ordinal))
                .Select(request => request.DriverRouteId)
                .ToHashSet(StringComparer.Ordinal);
            var matches = await _matchingService.FindMatchesAsync(
                _passengerRoute,
                driverRoutes.Where(route =>
                    !unavailableDriverRouteIds.Contains(route.Id)),
                cancellationToken);

            foreach (var match in matches)
            {
                Results.Add(new MatchResultItemViewModel(match));
            }

            IsEmpty = Results.Count == 0;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            SetError(
                exception.Message,
                "Não foi possível buscar rotas compatíveis. Tente novamente.");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void RequestRide(MatchResultItemViewModel? result)
    {
        if (result is null || _passengerRoute is null || IsBusy)
        {
            return;
        }

        RideRequestRequested?.Invoke(_passengerRoute, result);
    }

    [RelayCommand]
    private void ViewRoute(MatchResultItemViewModel? result)
    {
        if (result is null || _passengerRoute is null || IsBusy)
        {
            return;
        }

        RouteDetailsRequested?.Invoke(_passengerRoute, result);
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

public sealed class MatchResultItemViewModel
{
    private static readonly CultureInfo PtBrCulture =
        CultureInfo.GetCultureInfo("pt-BR");

    public MatchResultItemViewModel(MatchResult match)
    {
        Match = match ?? throw new ArgumentNullException(nameof(match));
    }

    public MatchResult Match { get; }

    public string DriverNameText =>
        string.IsNullOrWhiteSpace(Match.DriverRoute.UserName)
            ? "Motorista"
            : Match.DriverRoute.UserName;

    public string OriginDestinationText =>
        RoutePresentationText.GetOriginDestinationText(Match.DriverRoute);

    public string CompatibleDaysText =>
        RoutePresentationText.GetDaysText(Match.CompatibleDays);

    public string DepartureTimeText =>
        RoutePresentationText.GetDepartureTimeText(
            Match.DriverRoute.DepartureTimeMinutes);

    public string AvailableSeatsText =>
        RoutePresentationText.GetAvailableSeatsText(
            Match.DriverRoute.AvailableSeats);

    public string DetourText =>
        $"Desvio: +{Match.DetourDistanceKm.ToString("0.#", PtBrCulture)} km"
        + $" · +{Math.Ceiling(Match.DetourDurationMinutes).ToString(
            "0",
            PtBrCulture)} min";
}
