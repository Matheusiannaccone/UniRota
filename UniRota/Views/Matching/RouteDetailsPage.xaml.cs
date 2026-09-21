using Microsoft.Maui.Controls.Maps;
using Microsoft.Maui.Maps;
using UniRota.Models;
using UniRota.ViewModels;

namespace UniRota.Views.Matching;

public partial class RouteDetailsPage : ContentPage, IQueryAttributable
{
    public const string PassengerRouteParameterName = "PassengerRoute";
    public const string MatchParameterName = "Match";

    private readonly RouteDetailsViewModel _viewModel;

    public RouteDetailsPage(RouteDetailsViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        _viewModel.SetRouteContext(
            query.TryGetValue(PassengerRouteParameterName, out var routeValue)
                && routeValue is WeeklyRoute passengerRoute
                    ? passengerRoute
                    : null,
            query.TryGetValue(MatchParameterName, out var matchValue)
                && matchValue is MatchResult match
                    ? match
                    : null);
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadCommand.ExecuteAsync(null);
        RenderMap();
    }

    private void RenderMap()
    {
        RouteMap.MapElements.Clear();
        RouteMap.Pins.Clear();

        if (!_viewModel.HasMapData
            || _viewModel.Viewport is null
            || _viewModel.RoutePoints.Count < 2)
        {
            return;
        }

        try
        {
            var routeLine = new Polyline { StrokeWidth = 6 };
            routeLine.SetDynamicResource(Polyline.StrokeColorProperty, "Primary");

            foreach (var point in _viewModel.RoutePoints)
            {
                routeLine.Geopath.Add(ToLocation(point));
            }

            RouteMap.MapElements.Add(routeLine);

            foreach (var pin in _viewModel.Pins)
            {
                RouteMap.Pins.Add(new Pin
                {
                    Label = pin.Label,
                    Address = pin.Address,
                    Location = ToLocation(pin.Coordinate),
                    Type = PinType.Place
                });
            }

            var viewport = _viewModel.Viewport;
            RouteMap.MoveToRegion(new MapSpan(
                ToLocation(viewport.Center),
                viewport.LatitudeDegrees,
                viewport.LongitudeDegrees));
        }
        catch (Exception)
        {
            RouteMap.MapElements.Clear();
            RouteMap.Pins.Clear();
            _viewModel.ReportRenderingFailure();
        }
    }

    private static Location ToLocation(MapCoordinate coordinate)
    {
        return new Location(coordinate.Latitude, coordinate.Longitude);
    }
}
