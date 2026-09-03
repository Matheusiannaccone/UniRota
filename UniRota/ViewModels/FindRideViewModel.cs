using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using UniRota.Models;
using UniRota.Services.Interfaces;

namespace UniRota.ViewModels;

public partial class FindRideViewModel : ObservableObject
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

    public FindRideViewModel(IRouteService routeService)
    {
        _routeService = routeService;
    }

    public event Action<WeeklyRouteItemViewModel>? FindMatchesRequested;

    public ObservableCollection<WeeklyRouteItemViewModel> PassengerRoutes { get; } = [];

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

            PassengerRoutes.Clear();

            foreach (var route in routes.Where(
                         route => route.Role == RouteRole.Passenger))
            {
                PassengerRoutes.Add(new WeeklyRouteItemViewModel(route));
            }

            IsEmpty = PassengerRoutes.Count == 0;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            SetError(
                exception.Message,
                "Não foi possível carregar suas rotas de passageiro. Tente novamente.");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void FindMatches(WeeklyRouteItemViewModel? route)
    {
        if (route is null || IsBusy)
        {
            return;
        }

        FindMatchesRequested?.Invoke(route);
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
