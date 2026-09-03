using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using UniRota.Models;
using UniRota.Services.Interfaces;

namespace UniRota.ViewModels;

public partial class MyRoutesViewModel : ObservableObject
{
    private readonly IRouteService _routeService;

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private string errorMessage = string.Empty;

    [ObservableProperty]
    private bool hasError;

    [ObservableProperty]
    private bool isEmpty = true;

    public MyRoutesViewModel(IRouteService routeService)
    {
        _routeService = routeService;
    }

    public event Action<WeeklyRouteItemViewModel>? EditRequested;

    public ObservableCollection<WeeklyRouteItemViewModel> Routes { get; } = [];

    public bool IsNotBusy => !IsBusy;

    public bool ShowEmptyState => IsEmpty && !HasError && !IsBusy;

    partial void OnIsBusyChanged(bool value)
    {
        OnPropertyChanged(nameof(IsNotBusy));
        OnPropertyChanged(nameof(ShowEmptyState));
    }

    partial void OnHasErrorChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowEmptyState));
    }

    partial void OnIsEmptyChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowEmptyState));
    }

    [RelayCommand(AllowConcurrentExecutions = false)]
    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        ClearError();

        try
        {
            var routes = await _routeService.GetMyRoutesAsync(cancellationToken);

            Routes.Clear();

            foreach (var route in routes)
            {
                Routes.Add(new WeeklyRouteItemViewModel(route));
            }

            IsEmpty = Routes.Count == 0;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            SetError(
                exception.Message,
                "Não foi possível carregar suas rotas. Tente novamente.");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void Edit(WeeklyRouteItemViewModel? route)
    {
        if (route is null || IsBusy)
        {
            return;
        }

        EditRequested?.Invoke(route);
    }

    [RelayCommand(AllowConcurrentExecutions = false)]
    private async Task DeleteAsync(
        WeeklyRouteItemViewModel? route,
        CancellationToken cancellationToken)
    {
        if (route is null || IsBusy)
        {
            return;
        }

        IsBusy = true;
        ClearError();

        try
        {
            await _routeService.DeleteAsync(route.Route.Id, cancellationToken);
            Routes.Remove(route);
            IsEmpty = Routes.Count == 0;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            SetError(
                exception.Message,
                "Não foi possível excluir a rota. Tente novamente.");
        }
        finally
        {
            IsBusy = false;
        }
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
}

public sealed class WeeklyRouteItemViewModel
{
    public WeeklyRouteItemViewModel(WeeklyRoute route)
    {
        Route = route ?? throw new ArgumentNullException(nameof(route));
    }

    public WeeklyRoute Route { get; }

    public string RoleText => RoutePresentationText.GetRoleName(Route.Role);

    public string OriginDestinationText =>
        RoutePresentationText.GetOriginDestinationText(Route);

    public string DaysText => RoutePresentationText.GetDaysText(Route.DaysOfWeek);

    public string DepartureTimeText =>
        RoutePresentationText.GetDepartureTimeText(Route.DepartureTimeMinutes);

    public bool HasAvailableSeats =>
        Route.Role == RouteRole.Driver && Route.AvailableSeats is > 0;

    public string AvailableSeatsText =>
        RoutePresentationText.GetAvailableSeatsText(Route.AvailableSeats);
}
