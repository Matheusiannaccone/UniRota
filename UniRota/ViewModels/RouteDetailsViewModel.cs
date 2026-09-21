using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using UniRota.Models;
using UniRota.Services;
using UniRota.Services.Interfaces;

namespace UniRota.ViewModels;

public partial class RouteDetailsViewModel : ObservableObject
{
    private static readonly CultureInfo PtBrCulture =
        CultureInfo.GetCultureInfo("pt-BR");

    private readonly IPlaceService _placeService;
    private WeeklyRoute? _passengerRoute;
    private MatchResult? _match;
    private bool _hasLoaded;

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private bool hasMapData;

    [ObservableProperty]
    private bool hasMapMessage;

    [ObservableProperty]
    private string mapMessage = string.Empty;

    [ObservableProperty]
    private string distanceText = string.Empty;

    [ObservableProperty]
    private string durationText = string.Empty;

    [ObservableProperty]
    private string detourText = string.Empty;

    [ObservableProperty]
    private string driverRouteText = string.Empty;

    public RouteDetailsViewModel(IPlaceService placeService)
    {
        _placeService = placeService;
    }

    public IReadOnlyList<MapCoordinate> RoutePoints { get; private set; } = [];

    public IReadOnlyList<RouteMapPin> Pins { get; private set; } = [];

    public MapViewport? Viewport { get; private set; }

    public bool ShowMapPlaceholder => !HasMapData && !IsBusy;

    partial void OnIsBusyChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowMapPlaceholder));
    }

    partial void OnHasMapDataChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowMapPlaceholder));
    }

    public void SetRouteContext(
        WeeklyRoute? passengerRoute,
        MatchResult? match)
    {
        _passengerRoute = passengerRoute;
        _match = match;
        _hasLoaded = false;
        IsBusy = false;
        HasMapData = false;
        SetMapMessage(string.Empty);
        RoutePoints = [];
        Pins = [];
        Viewport = null;

        DriverRouteText = match is null
            ? string.Empty
            : RoutePresentationText.GetOriginDestinationText(
                match.DriverRoute);
        DistanceText = match is null
            ? string.Empty
            : $"Distância total: {FormatMetric(match.SharedDistanceKm)} km";
        DurationText = match is null
            ? string.Empty
            : $"Duração: {FormatMetric(match.SharedDurationMinutes)} min";
        DetourText = match is null
            ? string.Empty
            : $"Desvio: +{FormatMetric(match.DetourDistanceKm)} km"
              + $" · +{FormatMetric(match.DetourDurationMinutes)} min";
    }

    [RelayCommand(AllowConcurrentExecutions = false)]
    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        if (IsBusy || _hasLoaded)
        {
            return;
        }

        _hasLoaded = true;
        IsBusy = true;
        HasMapData = false;
        SetMapMessage(string.Empty);

        try
        {
            if (_passengerRoute is null || _match is null)
            {
                SetMapUnavailable(
                    "Não foi possível identificar o trajeto selecionado.");
                return;
            }

            var routePoints = EncodedPolylineDecoder.Decode(
                _match.SharedEncodedPolyline);

            if (routePoints.Count < 2)
            {
                SetMapUnavailable(
                    "Não foi possível exibir o trajeto no mapa.");
                return;
            }

            RoutePoints = routePoints;
            Viewport = MapViewportCalculator.Calculate(routePoints);
            HasMapData = true;

            try
            {
                var pinSpecifications = CreatePinSpecifications(
                    _passengerRoute,
                    _match.DriverRoute);
                var coordinates = await _placeService.GetCoordinatesAsync(
                    pinSpecifications
                        .Select(specification => specification.PlaceId)
                        .ToArray(),
                    cancellationToken);
                Pins = CreatePins(pinSpecifications, coordinates);

                if (Pins.Count < pinSpecifications.Count)
                {
                    SetMapMessage(
                        "Alguns pontos do trajeto não puderam ser posicionados.");
                }
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception)
            {
                Pins = [];
                SetMapMessage(
                    "Não foi possível posicionar os pontos do trajeto.");
            }
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            _hasLoaded = false;
        }
        catch (FormatException)
        {
            SetMapUnavailable("Não foi possível exibir o trajeto no mapa.");
        }
        finally
        {
            IsBusy = false;
        }
    }

    public void ReportRenderingFailure()
    {
        SetMapUnavailable("Não foi possível exibir o trajeto no mapa.");
    }

    private static IReadOnlyList<PinSpecification> CreatePinSpecifications(
        WeeklyRoute passengerRoute,
        WeeklyRoute driverRoute)
    {
        var candidates = new[]
        {
            new PinSpecification(
                driverRoute.OriginPlaceId,
                "Origem do motorista",
                driverRoute.Origin),
            new PinSpecification(
                passengerRoute.OriginPlaceId,
                "Origem do passageiro",
                passengerRoute.Origin),
            new PinSpecification(
                passengerRoute.DestinationPlaceId,
                "Destino do passageiro",
                passengerRoute.Destination),
            new PinSpecification(
                driverRoute.DestinationPlaceId,
                "Destino do motorista",
                driverRoute.Destination)
        };
        var grouped = new Dictionary<string, List<PinSpecification>>(
            StringComparer.Ordinal);

        foreach (var candidate in candidates)
        {
            var placeId = candidate.PlaceId?.Trim() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(placeId))
            {
                continue;
            }

            if (!grouped.TryGetValue(placeId, out var specifications))
            {
                specifications = [];
                grouped.Add(placeId, specifications);
            }

            specifications.Add(candidate with { PlaceId = placeId });
        }

        return grouped.Select(group => new PinSpecification(
                group.Key,
                string.Join(
                    " · ",
                    group.Value
                        .Select(specification => specification.Label)
                        .Distinct(StringComparer.Ordinal)),
                string.Join(
                    " · ",
                    group.Value
                        .Select(specification => specification.Address?.Trim())
                        .Where(address => !string.IsNullOrWhiteSpace(address))
                        .Distinct(StringComparer.Ordinal))))
            .ToArray();
    }

    private static IReadOnlyList<RouteMapPin> CreatePins(
        IReadOnlyList<PinSpecification> specifications,
        IReadOnlyDictionary<string, MapCoordinate> coordinates)
    {
        return specifications
            .Where(specification => coordinates.TryGetValue(
                specification.PlaceId,
                out var coordinate) && coordinate.IsValid)
            .Select(specification => new RouteMapPin(
                coordinates[specification.PlaceId],
                specification.Label,
                specification.Address))
            .ToArray();
    }

    private static string FormatMetric(decimal value)
    {
        return value.ToString("0.#", PtBrCulture);
    }

    private void SetMapUnavailable(string message)
    {
        HasMapData = false;
        RoutePoints = [];
        Pins = [];
        Viewport = null;
        SetMapMessage(message);
    }

    private void SetMapMessage(string message)
    {
        MapMessage = message;
        HasMapMessage = !string.IsNullOrWhiteSpace(message);
    }

    private sealed record PinSpecification(
        string PlaceId,
        string Label,
        string Address);
}

public sealed record RouteMapPin(
    MapCoordinate Coordinate,
    string Label,
    string Address);
