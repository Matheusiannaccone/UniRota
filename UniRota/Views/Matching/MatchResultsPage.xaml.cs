using UniRota.Models;
using UniRota.ViewModels;

namespace UniRota.Views.Matching;

public partial class MatchResultsPage : ContentPage, IQueryAttributable
{
    public const string PassengerRouteParameterName = "PassengerRoute";

    private readonly MatchResultsViewModel _viewModel;
    private bool _isNavigatingToRequest;
    private bool _isNavigatingToDetails;
    private bool _skipNextLoad;

    public MatchResultsPage(MatchResultsViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
        _viewModel.RideRequestRequested += OnRideRequestRequested;
        _viewModel.RouteDetailsRequested += OnRouteDetailsRequested;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        _viewModel.SetPassengerRoute(
            query.TryGetValue(PassengerRouteParameterName, out var value)
                && value is WeeklyRoute passengerRoute
                    ? passengerRoute
                    : null);
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (_skipNextLoad)
        {
            _skipNextLoad = false;
            return;
        }

        await _viewModel.LoadCommand.ExecuteAsync(null);
    }

    private void OnViewRouteClicked(object sender, EventArgs e)
    {
        if (sender is Button { CommandParameter: MatchResultItemViewModel result })
        {
            _viewModel.ViewRouteCommand.Execute(result);
        }
    }

    private void OnRequestRideClicked(object sender, EventArgs e)
    {
        if (sender is Button { CommandParameter: MatchResultItemViewModel result })
        {
            _viewModel.RequestRideCommand.Execute(result);
        }
    }

    private async void OnRideRequestRequested(
        WeeklyRoute passengerRoute,
        MatchResultItemViewModel result)
    {
        if (_isNavigatingToRequest || _viewModel.IsBusy)
        {
            return;
        }

        _isNavigatingToRequest = true;

        try
        {
            await Shell.Current.GoToAsync(
                nameof(RideRequestPage),
                new Dictionary<string, object>
                {
                    [RideRequestPage.PassengerRouteParameterName] = passengerRoute,
                    [RideRequestPage.MatchParameterName] = result.Match
                });
        }
        finally
        {
            _isNavigatingToRequest = false;
        }
    }

    private async void OnRouteDetailsRequested(
        WeeklyRoute passengerRoute,
        MatchResultItemViewModel result)
    {
        if (_isNavigatingToDetails || _viewModel.IsBusy)
        {
            return;
        }

        _isNavigatingToDetails = true;
        _skipNextLoad = true;

        try
        {
            await Shell.Current.GoToAsync(
                nameof(RouteDetailsPage),
                new Dictionary<string, object>
                {
                    [RouteDetailsPage.PassengerRouteParameterName] =
                        passengerRoute,
                    [RouteDetailsPage.MatchParameterName] = result.Match
                });
        }
        catch
        {
            _skipNextLoad = false;
            throw;
        }
        finally
        {
            _isNavigatingToDetails = false;
        }
    }
}
