using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using UniRota.Models;
using UniRota.Services.Interfaces;

namespace UniRota.ViewModels;

public partial class NewRouteViewModel : ObservableObject
{
    private static readonly TimeSpan DefaultDepartureTime = new(7, 0, 0);

    private readonly IRouteService _routeService;

    [ObservableProperty]
    private RouteRoleOption? selectedRole;

    [ObservableProperty]
    private string origin = string.Empty;

    [ObservableProperty]
    private string destination = string.Empty;

    [ObservableProperty]
    private TimeSpan departureTime = DefaultDepartureTime;

    [ObservableProperty]
    private int? availableSeats;

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private string errorMessage = string.Empty;

    [ObservableProperty]
    private bool hasError;

    [ObservableProperty]
    private string successMessage = string.Empty;

    [ObservableProperty]
    private bool hasSavedSuccessfully;

    public NewRouteViewModel(IRouteService routeService)
    {
        _routeService = routeService;

        RoleOptions =
        [
            new RouteRoleOption(RouteRole.Driver, "Motorista"),
            new RouteRoleOption(RouteRole.Passenger, "Passageiro")
        ];

        Days =
        [
            CreateSelectableDay(DayOfWeek.Monday),
            CreateSelectableDay(DayOfWeek.Tuesday),
            CreateSelectableDay(DayOfWeek.Wednesday),
            CreateSelectableDay(DayOfWeek.Thursday),
            CreateSelectableDay(DayOfWeek.Friday),
            CreateSelectableDay(DayOfWeek.Saturday),
            CreateSelectableDay(DayOfWeek.Sunday)
        ];
    }

    public IReadOnlyList<RouteRoleOption> RoleOptions { get; }

    public IReadOnlyList<SelectableDayViewModel> Days { get; }

    public bool IsDriver => SelectedRole?.Role == RouteRole.Driver;

    public bool IsNotBusy => !IsBusy;

    partial void OnSelectedRoleChanged(RouteRoleOption? value)
    {
        OnPropertyChanged(nameof(IsDriver));

        if (!IsDriver)
        {
            AvailableSeats = null;
        }
    }

    partial void OnIsBusyChanged(bool value)
    {
        OnPropertyChanged(nameof(IsNotBusy));
    }

    [RelayCommand(AllowConcurrentExecutions = false)]
    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        if (IsBusy)
        {
            return;
        }

        ClearFeedback();

        if (!TryBuildRoute(out var route))
        {
            return;
        }

        IsBusy = true;

        try
        {
            await _routeService.CreateAsync(route, cancellationToken);
            ResetForm();
            SuccessMessage = "Rota cadastrada com sucesso.";
            HasSavedSuccessfully = true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            SetError(exception.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool TryBuildRoute(out WeeklyRoute route)
    {
        route = new WeeklyRoute();

        if (SelectedRole is null
            || !Enum.IsDefined(typeof(RouteRole), SelectedRole.Role))
        {
            SetError("Selecione se você será motorista ou passageiro.");
            return false;
        }

        var normalizedOrigin = Origin?.Trim() ?? string.Empty;
        var normalizedDestination = Destination?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(normalizedOrigin))
        {
            SetError("Informe a origem da rota.");
            return false;
        }

        if (string.IsNullOrWhiteSpace(normalizedDestination))
        {
            SetError("Informe o destino da rota.");
            return false;
        }

        if (string.Equals(
                normalizedOrigin,
                normalizedDestination,
                StringComparison.OrdinalIgnoreCase))
        {
            SetError("A origem e o destino devem ser diferentes.");
            return false;
        }

        var selectedDays = Days
            .Where(day => day.IsSelected)
            .Select(day => day.Day)
            .ToArray();

        if (selectedDays.Length == 0)
        {
            SetError("Selecione ao menos um dia da semana.");
            return false;
        }

        if (DepartureTime < TimeSpan.Zero
            || DepartureTime >= TimeSpan.FromDays(1))
        {
            SetError("Informe um horário de saída válido.");
            return false;
        }

        if (SelectedRole.Role == RouteRole.Driver
            && AvailableSeats is null or <= 0)
        {
            SetError("Informe ao menos uma vaga para a rota de motorista.");
            return false;
        }

        route = new WeeklyRoute
        {
            Role = SelectedRole.Role,
            Origin = normalizedOrigin,
            Destination = normalizedDestination,
            DaysOfWeek = selectedDays,
            DepartureTimeMinutes = (int)DepartureTime.TotalMinutes,
            AvailableSeats = SelectedRole.Role == RouteRole.Driver
                ? AvailableSeats
                : null
        };

        return true;
    }

    private void ResetForm()
    {
        SelectedRole = null;
        Origin = string.Empty;
        Destination = string.Empty;
        DepartureTime = DefaultDepartureTime;
        AvailableSeats = null;

        foreach (var day in Days)
        {
            day.IsSelected = false;
        }
    }

    private void ClearFeedback()
    {
        ErrorMessage = string.Empty;
        HasError = false;
        SuccessMessage = string.Empty;
        HasSavedSuccessfully = false;
    }

    private void SetError(string message)
    {
        ErrorMessage = string.IsNullOrWhiteSpace(message)
            ? "Não foi possível cadastrar a rota. Tente novamente."
            : message;
        HasError = true;
    }

    private static SelectableDayViewModel CreateSelectableDay(DayOfWeek day)
    {
        return new SelectableDayViewModel(day, RoutePresentationText.GetDayName(day));
    }
}

public sealed record RouteRoleOption(RouteRole Role, string DisplayName);

public partial class SelectableDayViewModel : ObservableObject
{
    [ObservableProperty]
    private bool isSelected;

    public SelectableDayViewModel(DayOfWeek day, string displayName)
    {
        Day = day;
        DisplayName = displayName;
    }

    public DayOfWeek Day { get; }

    public string DisplayName { get; }
}

internal static class RoutePresentationText
{
    public static string GetRoleName(RouteRole role)
    {
        return role switch
        {
            RouteRole.Driver => "Motorista",
            RouteRole.Passenger => "Passageiro",
            _ => string.Empty
        };
    }

    public static string GetDayName(DayOfWeek day)
    {
        return day switch
        {
            DayOfWeek.Monday => "Segunda-feira",
            DayOfWeek.Tuesday => "Terça-feira",
            DayOfWeek.Wednesday => "Quarta-feira",
            DayOfWeek.Thursday => "Quinta-feira",
            DayOfWeek.Friday => "Sexta-feira",
            DayOfWeek.Saturday => "Sábado",
            DayOfWeek.Sunday => "Domingo",
            _ => string.Empty
        };
    }
}
