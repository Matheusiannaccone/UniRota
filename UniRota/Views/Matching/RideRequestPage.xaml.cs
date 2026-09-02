using UniRota.Models;
using UniRota.ViewModels;

namespace UniRota.Views.Matching;

public partial class RideRequestPage : ContentPage, IQueryAttributable
{
    public const string PassengerRouteParameterName = "PassengerRoute";
    public const string MatchParameterName = "Match";

    private readonly RideRequestViewModel _viewModel;
    private bool _isConfirming;

    public RideRequestPage(RideRequestViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        _viewModel.SetRequestContext(
            query.TryGetValue(PassengerRouteParameterName, out var routeValue)
                && routeValue is WeeklyRoute passengerRoute
                    ? passengerRoute
                    : null,
            query.TryGetValue(MatchParameterName, out var matchValue)
                && matchValue is MatchResult match
                    ? match
                    : null);
    }

    private async void OnConfirmRequestClicked(object sender, EventArgs e)
    {
        if (_isConfirming
            || _viewModel.IsBusy
            || !_viewModel.TryGetConfirmationMessage(out var message))
        {
            return;
        }

        _isConfirming = true;

        try
        {
            var confirmed = await DisplayAlert(
                "Confirmar solicitação",
                message,
                "Enviar",
                "Voltar");

            if (!confirmed)
            {
                return;
            }

            await _viewModel.SubmitCommand.ExecuteAsync(null);

            if (!_viewModel.HasSubmittedSuccessfully)
            {
                return;
            }

            await DisplayAlert(
                "Solicitação enviada",
                "Solicitação enviada ao motorista.",
                "OK");

            try
            {
                await Shell.Current.GoToAsync("..");
            }
            catch (Exception)
            {
                await DisplayAlert(
                    "Solicitação enviada",
                    "A solicitação foi criada, mas não foi possível voltar automaticamente.",
                    "OK");
            }
        }
        finally
        {
            _isConfirming = false;
        }
    }
}
