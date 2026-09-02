using UniRota.ViewModels;

namespace UniRota.Views.Matching;

public partial class ReceivedRequestsPage : ContentPage
{
    private readonly ReceivedRequestsViewModel _viewModel;
    private bool _isConfirmingAction;

    public ReceivedRequestsPage(ReceivedRequestsViewModel viewModel)
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

    private async void OnAcceptClicked(object sender, EventArgs e)
    {
        if (!TryGetRequest(sender, out var item))
        {
            return;
        }

        _isConfirmingAction = true;

        try
        {
            var confirmed = await DisplayAlert(
                "Aceitar solicitação?",
                "Uma vaga da rota será consumida.",
                "Aceitar",
                "Voltar");

            if (confirmed)
            {
                await _viewModel.AcceptCommand.ExecuteAsync(item);
            }
        }
        finally
        {
            _isConfirmingAction = false;
        }
    }

    private async void OnRejectClicked(object sender, EventArgs e)
    {
        if (!TryGetRequest(sender, out var item))
        {
            return;
        }

        _isConfirmingAction = true;

        try
        {
            var confirmed = await DisplayAlert(
                "Recusar solicitação?",
                "A solicitação será recusada e as vagas não serão alteradas.",
                "Recusar",
                "Voltar");

            if (confirmed)
            {
                await _viewModel.RejectCommand.ExecuteAsync(item);
            }
        }
        finally
        {
            _isConfirmingAction = false;
        }
    }

    private bool TryGetRequest(
        object sender,
        out RideRequestItemViewModel? item)
    {
        item = (sender as Button)?.CommandParameter
            as RideRequestItemViewModel;

        return !_isConfirmingAction
               && !_viewModel.IsBusy
               && item is not null;
    }
}
