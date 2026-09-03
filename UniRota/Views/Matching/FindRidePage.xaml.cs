using UniRota.ViewModels;

namespace UniRota.Views.Matching;

public partial class FindRidePage : ContentPage
{
    private readonly FindRideViewModel _viewModel;
    private bool _isNavigatingToResults;

    public FindRidePage(FindRideViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
        _viewModel.FindMatchesRequested += OnFindMatchesRequested;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadCommand.ExecuteAsync(null);
    }

    private void OnFindMatchesClicked(object sender, EventArgs e)
    {
        if (sender is Button { CommandParameter: WeeklyRouteItemViewModel route })
        {
            _viewModel.FindMatchesCommand.Execute(route);
        }
    }

    private async void OnFindMatchesRequested(WeeklyRouteItemViewModel route)
    {
        if (_isNavigatingToResults || _viewModel.IsBusy)
        {
            return;
        }

        _isNavigatingToResults = true;

        try
        {
            await Shell.Current.GoToAsync(
                nameof(MatchResultsPage),
                new Dictionary<string, object>
                {
                    [MatchResultsPage.PassengerRouteParameterName] = route.Route
                });
        }
        finally
        {
            _isNavigatingToResults = false;
        }
    }
}
