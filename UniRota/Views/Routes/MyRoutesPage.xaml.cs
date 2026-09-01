using UniRota.ViewModels;

namespace UniRota.Views.Routes;

public partial class MyRoutesPage : ContentPage
{
    private readonly MyRoutesViewModel _viewModel;
    private bool _isNavigatingToNewRoute;

    public MyRoutesPage(MyRoutesViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadCommand.ExecuteAsync(null);
    }

    private async void OnNewRouteClicked(object sender, EventArgs e)
    {
        if (_isNavigatingToNewRoute || _viewModel.IsBusy)
        {
            return;
        }

        _isNavigatingToNewRoute = true;

        try
        {
            await Shell.Current.GoToAsync(nameof(NewRoutePage));
        }
        finally
        {
            _isNavigatingToNewRoute = false;
        }
    }
}
