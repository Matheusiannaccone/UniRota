using UniRota.ViewModels;

namespace UniRota.Views.Matching;

public partial class ConfirmedRoutesPage : ContentPage
{
    private readonly ConfirmedRoutesViewModel _viewModel;

    public ConfirmedRoutesPage(ConfirmedRoutesViewModel viewModel)
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
}
