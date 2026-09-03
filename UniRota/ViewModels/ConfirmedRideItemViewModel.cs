using UniRota.Models;

namespace UniRota.ViewModels;

public sealed class ConfirmedRideItemViewModel
{
    public ConfirmedRideItemViewModel(
        RideRequest request,
        string currentUserId)
    {
        Request = request ?? throw new ArgumentNullException(nameof(request));

        if (string.IsNullOrWhiteSpace(currentUserId))
        {
            throw new ArgumentException(
                "Informe o identificador do usuário autenticado.",
                nameof(currentUserId));
        }

        if (string.Equals(
                request.PassengerUserId,
                currentUserId,
                StringComparison.Ordinal))
        {
            CurrentUserRoleText = "Você é o passageiro";
        }
        else if (string.Equals(
                     request.DriverUserId,
                     currentUserId,
                     StringComparison.Ordinal))
        {
            CurrentUserRoleText = "Você é o motorista";
        }
        else
        {
            throw new ArgumentException(
                "A solicitação não pertence ao usuário autenticado.",
                nameof(request));
        }

        PassengerNameText = GetNameOrFallback(
            request.PassengerUserName,
            "Passageiro");
        DriverNameText = GetNameOrFallback(
            request.DriverUserName,
            "Motorista");
    }

    public RideRequest Request { get; }

    public string PassengerNameText { get; }

    public string DriverNameText { get; }

    public string CurrentUserRoleText { get; }

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

    public string StatusText => "Confirmada";

    private static string GetNameOrFallback(
        string name,
        string fallback)
    {
        return string.IsNullOrWhiteSpace(name)
            ? fallback
            : name;
    }
}
