using UniRota.ViewModels;

namespace UniRota.Views.Matching;

public partial class AwaitingApprovalPage : ContentPage
{
    private readonly AwaitingApprovalViewModel _viewModel;

    public AwaitingApprovalPage(AwaitingApprovalViewModel viewModel)
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
