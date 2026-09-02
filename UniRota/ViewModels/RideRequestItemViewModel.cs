using UniRota.Models;

namespace UniRota.ViewModels;

public sealed class RideRequestItemViewModel
{
    public RideRequestItemViewModel(
        RideRequest request,
        string counterpartyName,
        string counterpartyFallback)
    {
        Request = request ?? throw new ArgumentNullException(nameof(request));
        CounterpartyNameText = string.IsNullOrWhiteSpace(counterpartyName)
            ? counterpartyFallback
            : counterpartyName;
    }

    public RideRequest Request { get; }

    public string CounterpartyNameText { get; }

    public string TypeText => Request.Type switch
    {
        RideRequestType.Once => "Uma vez",
        RideRequestType.Weekly => "Semanal",
        _ => string.Empty
    };

    public string CompatibleDaysText =>
        RoutePresentationText.GetDaysText(Request.CompatibleDays);

    public bool HasRequestedDate => Request.RequestedDate is not null;

    public string RequestedDateText => Request.RequestedDate is not null
        ? Request.RequestedDate.Value.ToString("dd/MM/yyyy")
        : string.Empty;

    public string StatusText => "Aguardando aceite";
}
