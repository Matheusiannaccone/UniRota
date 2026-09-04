using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using UniRota.Models;
using UniRota.Services;
using UniRota.Services.Interfaces;

namespace UniRota.ViewModels;

public partial class RideRequestViewModel : ObservableObject
{
    private readonly IRideRequestService _rideRequestService;
    private readonly IPricingService _pricingService;
    private WeeklyRoute? _passengerRoute;
    private MatchResult? _match;
    private PricingResult? _pricingResult;

    private static readonly CultureInfo PtBrCulture =
        CultureInfo.GetCultureInfo("pt-BR");

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private string errorMessage = string.Empty;

    [ObservableProperty]
    private bool hasError;

    [ObservableProperty]
    private RideRequestTypeOption? selectedRequestType;

    [ObservableProperty]
    private DateTime requestedDate = DateTime.Today;

    [ObservableProperty]
    private string driverNameText = string.Empty;

    [ObservableProperty]
    private string originDestinationText = string.Empty;

    [ObservableProperty]
    private string compatibleDaysText = string.Empty;

    [ObservableProperty]
    private string suggestedPriceText = string.Empty;

    [ObservableProperty]
    private bool hasSuggestedPrice;

    [ObservableProperty]
    private bool hasSubmittedSuccessfully;

    public RideRequestViewModel(
        IRideRequestService rideRequestService,
        IPricingService pricingService)
    {
        _rideRequestService = rideRequestService;
        _pricingService = pricingService;
    }

    public IReadOnlyList<RideRequestTypeOption> RequestTypes { get; } =
    [
        new(RideRequestType.Once, "Uma vez"),
        new(RideRequestType.Weekly, "Semanal")
    ];

    public DateTime MinimumDate => DateTime.Today;

    public bool IsNotBusy => !IsBusy;

    public bool CanSubmit => IsNotBusy && !HasSubmittedSuccessfully;

    public bool IsOnceSelected =>
        SelectedRequestType?.Type == RideRequestType.Once;

    public bool IsWeeklySelected =>
        SelectedRequestType?.Type == RideRequestType.Weekly;

    partial void OnIsBusyChanged(bool value)
    {
        OnPropertyChanged(nameof(IsNotBusy));
        OnPropertyChanged(nameof(CanSubmit));
    }

    partial void OnHasSubmittedSuccessfullyChanged(bool value)
    {
        OnPropertyChanged(nameof(CanSubmit));
    }

    partial void OnSelectedRequestTypeChanged(RideRequestTypeOption? value)
    {
        ClearError();
        OnPropertyChanged(nameof(IsOnceSelected));
        OnPropertyChanged(nameof(IsWeeklySelected));
    }

    public void SetRequestContext(
        WeeklyRoute? passengerRoute,
        MatchResult? match)
    {
        _passengerRoute = passengerRoute;
        _match = match;
        _pricingResult = null;
        SelectedRequestType = null;
        RequestedDate = DateTime.Today;
        HasSubmittedSuccessfully = false;
        HasSuggestedPrice = false;
        SuggestedPriceText = string.Empty;
        ClearError();

        DriverNameText = match is null
            || string.IsNullOrWhiteSpace(match.DriverRoute.UserName)
                ? "Motorista"
                : match.DriverRoute.UserName;
        OriginDestinationText = match is null
            ? string.Empty
            : RoutePresentationText.GetOriginDestinationText(match.DriverRoute);
        CompatibleDaysText = match is null
            ? string.Empty
            : RoutePresentationText.GetDaysText(match.CompatibleDays);

        if (match is null)
        {
            return;
        }

        try
        {
            var pricingResult = _pricingService.Calculate(
                match.DriverRoute.EstimatedDistanceKm);

            if (pricingResult.SuggestedPrice <= 0m)
            {
                SetInvalidPricingError();
                return;
            }

            _pricingResult = pricingResult;
            SuggestedPriceText = $"Preço sugerido: R$ {pricingResult.SuggestedPrice.ToString(
                "N2",
                PtBrCulture)} por viagem";
            HasSuggestedPrice = true;
        }
        catch (ArgumentOutOfRangeException)
        {
            SetInvalidPricingError();
        }
    }

    public bool TryGetConfirmationMessage(out string message)
    {
        message = string.Empty;
        ClearError();

        if (_passengerRoute is null || _match is null)
        {
            SetError(
                "Não foi possível identificar as rotas selecionadas.",
                "Volte e selecione novamente uma rota compatível.");
            return false;
        }

        if (_pricingResult is null || _pricingResult.SuggestedPrice <= 0m)
        {
            SetInvalidPricingError();
            return false;
        }

        if (SelectedRequestType is null)
        {
            SetError(
                "Selecione se a carona acontecerá uma vez ou semanalmente.",
                "Selecione um tipo de solicitação.");
            return false;
        }

        var requestedDate = GetRequestedDate(SelectedRequestType.Type);

        try
        {
            RideRequestRules.ValidateForCreation(
                SelectedRequestType.Type,
                _match.CompatibleDays,
                requestedDate,
                DateOnly.FromDateTime(DateTime.Today));
        }
        catch (ArgumentException exception)
        {
            SetError(
                exception.Message,
                "Revise os dados da solicitação.");
            return false;
        }

        message = SelectedRequestType.Type == RideRequestType.Once
            ? $"Enviar solicitação para {DriverNameText} em "
              + $"{requestedDate!.Value:dd/MM/yyyy}?"
            : $"Enviar solicitação semanal para {DriverNameText} em: "
              + $"{CompatibleDaysText}?";

        return true;
    }

    [RelayCommand(AllowConcurrentExecutions = false)]
    private async Task SubmitAsync(CancellationToken cancellationToken)
    {
        if (IsBusy || HasSubmittedSuccessfully)
        {
            return;
        }

        if (_passengerRoute is null
            || _match is null
            || SelectedRequestType is null)
        {
            SetError(
                "Não foi possível identificar os dados da solicitação.",
                "Volte e selecione novamente uma rota compatível.");
            return;
        }

        if (_pricingResult is null || _pricingResult.SuggestedPrice <= 0m)
        {
            SetInvalidPricingError();
            return;
        }

        IsBusy = true;
        ClearError();

        try
        {
            var type = SelectedRequestType.Type;
            var requestedDate = GetRequestedDate(type);

            await _rideRequestService.CreateAsync(
                _passengerRoute.Id,
                _match,
                type,
                requestedDate,
                _pricingResult.SuggestedPrice,
                cancellationToken);

            HasSubmittedSuccessfully = true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            SetError(
                exception.Message,
                "Não foi possível enviar a solicitação. Tente novamente.");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private DateOnly? GetRequestedDate(RideRequestType type)
    {
        return type == RideRequestType.Once
            ? DateOnly.FromDateTime(RequestedDate)
            : null;
    }

    private void ClearError()
    {
        ErrorMessage = string.Empty;
        HasError = false;
    }

    private void SetError(string message, string fallbackMessage)
    {
        ErrorMessage = string.IsNullOrWhiteSpace(message)
            ? fallbackMessage
            : message;
        HasError = true;
    }

    private void SetInvalidPricingError()
    {
        SetError(
            "A rota do motorista não possui uma distância estimada válida para calcular o preço sugerido.",
            "Não foi possível calcular o preço sugerido para esta rota.");
    }
}

public sealed record RideRequestTypeOption(
    RideRequestType Type,
    string DisplayName);
