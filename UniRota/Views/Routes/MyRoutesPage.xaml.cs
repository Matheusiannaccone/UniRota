using UniRota.ViewModels;

namespace UniRota.Views.Routes;

public partial class MyRoutesPage : ContentPage
{
    private readonly MyRoutesViewModel _viewModel;
    private bool _isNavigatingToRouteForm;
    private bool _isConfirmingDelete;

    public MyRoutesPage(MyRoutesViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
        _viewModel.EditRequested += OnEditRequested;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadCommand.ExecuteAsync(null);
    }

    private async void OnNewRouteClicked(object sender, EventArgs e)
    {
        await NavigateToRouteFormAsync(null);
    }

    private void OnEditRouteClicked(object sender, EventArgs e)
    {
        if (sender is Button { CommandParameter: WeeklyRouteItemViewModel route })
        {
            _viewModel.EditCommand.Execute(route);
        }
    }

    private async void OnEditRequested(WeeklyRouteItemViewModel route)
    {
        await NavigateToRouteFormAsync(route);
    }

    private async void OnDeleteRouteClicked(object sender, EventArgs e)
    {
        if (_isConfirmingDelete
            || _viewModel.IsBusy
            || sender is not Button
            {
                CommandParameter: WeeklyRouteItemViewModel route
            })
        {
            return;
        }

        _isConfirmingDelete = true;

        try
        {
            var confirmed = await DisplayAlert(
                "Excluir esta rota?",
                "Esta ação não poderá ser desfeita.",
                "Excluir",
                "Cancelar");

            if (confirmed)
            {
                await _viewModel.DeleteCommand.ExecuteAsync(route);
            }
        }
        finally
        {
            _isConfirmingDelete = false;
        }
    }

    private async Task NavigateToRouteFormAsync(
        WeeklyRouteItemViewModel? route)
    {
        if (_isNavigatingToRouteForm || _viewModel.IsBusy)
        {
            return;
        }

        _isNavigatingToRouteForm = true;

        try
        {
            if (route is null)
            {
                await Shell.Current.GoToAsync(nameof(NewRoutePage));
                return;
            }

            await Shell.Current.GoToAsync(
                nameof(NewRoutePage),
                new Dictionary<string, object>
                {
                    [NewRoutePage.RouteParameterName] = route.Route
                });
        }
        finally
        {
            _isNavigatingToRouteForm = false;
        }
    }
}
