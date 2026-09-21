using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using UniRota.Models;
using UniRota.Services.Interfaces;

namespace UniRota.ViewModels;

public partial class NewRouteViewModel : ObservableObject
{
    private static readonly TimeSpan DefaultDepartureTime = new(7, 0, 0);
    private const int MinimumAutocompleteLength = 3;
    private static readonly TimeSpan AutocompleteDebounce =
        TimeSpan.FromMilliseconds(350);

    private readonly IRouteService _routeService;
    private readonly IPlaceService _placeService;
    private readonly IMapRouteService _mapRouteService;
    private WeeklyRoute? _routeBeingEdited;
    private PlaceAutocompleteSession? _originSession;
    private PlaceAutocompleteSession? _destinationSession;
    private CancellationTokenSource? _originSearchCancellation;
    private CancellationTokenSource? _destinationSearchCancellation;
    private int _originSearchVersion;
    private int _destinationSearchVersion;
    private bool _isApplyingOriginSelection;
    private bool _isApplyingDestinationSelection;

    [ObservableProperty]
    private RouteRoleOption? selectedRole;

    [ObservableProperty]
    private string origin = string.Empty;

    [ObservableProperty]
    private string originPlaceId = string.Empty;

    [ObservableProperty]
    private string destination = string.Empty;

    [ObservableProperty]
    private string destinationPlaceId = string.Empty;

    [ObservableProperty]
    private bool isSearchingOrigin;

    [ObservableProperty]
    private bool isSearchingDestination;

    [ObservableProperty]
    private string originSearchError = string.Empty;

    [ObservableProperty]
    private string destinationSearchError = string.Empty;

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

    [ObservableProperty]
    private bool isEditing;

    public NewRouteViewModel(
        IRouteService routeService,
        IPlaceService placeService,
        IMapRouteService mapRouteService)
    {
        _routeService = routeService;
        _placeService = placeService;
        _mapRouteService = mapRouteService;

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

    public ObservableCollection<PlaceSuggestion> OriginSuggestions { get; } = [];

    public ObservableCollection<PlaceSuggestion> DestinationSuggestions { get; } = [];

    public bool HasOriginSuggestions => OriginSuggestions.Count > 0;

    public bool HasDestinationSuggestions => DestinationSuggestions.Count > 0;

    public bool HasOriginSearchError => !string.IsNullOrWhiteSpace(OriginSearchError);

    public bool HasDestinationSearchError =>
        !string.IsNullOrWhiteSpace(DestinationSearchError);

    public bool IsDriver => SelectedRole?.Role == RouteRole.Driver;

    public bool IsNotBusy => !IsBusy;

    public string PageTitle => IsEditing ? "Editar rota" : "Nova rota";

    public string ActionButtonText =>
        IsEditing ? "Salvar alterações" : "Salvar rota";

    partial void OnOriginChanged(string value)
    {
        if (_isApplyingOriginSelection)
        {
            return;
        }

        OriginPlaceId = string.Empty;
        QueueOriginSearch(value);
    }

    partial void OnDestinationChanged(string value)
    {
        if (_isApplyingDestinationSelection)
        {
            return;
        }

        DestinationPlaceId = string.Empty;
        QueueDestinationSearch(value);
    }

    partial void OnOriginSearchErrorChanged(string value)
    {
        OnPropertyChanged(nameof(HasOriginSearchError));
    }

    partial void OnDestinationSearchErrorChanged(string value)
    {
        OnPropertyChanged(nameof(HasDestinationSearchError));
    }

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

    partial void OnIsEditingChanged(bool value)
    {
        OnPropertyChanged(nameof(PageTitle));
        OnPropertyChanged(nameof(ActionButtonText));
    }

    public void BeginCreate()
    {
        ClearFeedback();
        ResetForm();
    }

    public void BeginEdit(WeeklyRoute route)
    {
        ArgumentNullException.ThrowIfNull(route);

        if (string.IsNullOrWhiteSpace(route.Id))
        {
            throw new ArgumentException(
                "A rota selecionada não possui um identificador válido.",
                nameof(route));
        }

        var roleOption = RoleOptions.FirstOrDefault(
            option => option.Role == route.Role)
            ?? throw new ArgumentException(
                "A rota selecionada possui um papel inválido.",
                nameof(route));

        ClearFeedback();
        ResetPlaceSearches();
        _routeBeingEdited = route;
        IsEditing = true;
        SelectedRole = roleOption;
        SetOriginSelection(route.Origin, route.OriginPlaceId);
        SetDestinationSelection(route.Destination, route.DestinationPlaceId);
        DepartureTime = TimeSpan.FromMinutes(route.DepartureTimeMinutes);
        AvailableSeats = route.Role == RouteRole.Driver
            ? route.AvailableSeats
            : null;

        foreach (var day in Days)
        {
            day.IsSelected = route.DaysOfWeek.Contains(day.Day);
        }
    }

    [RelayCommand(AllowConcurrentExecutions = false)]
    private async Task SelectOriginAsync(
        PlaceSuggestion? suggestion,
        CancellationToken cancellationToken)
    {
        if (suggestion is null || _originSession is null)
        {
            return;
        }

        var session = _originSession;
        var requestCancellation =
            CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var version = ++_originSearchVersion;
        ReplaceCancellation(
            ref _originSearchCancellation,
            requestCancellation);
        OriginSuggestions.Clear();
        OnPropertyChanged(nameof(HasOriginSuggestions));
        OriginSearchError = string.Empty;
        IsSearchingOrigin = true;

        try
        {
            var selectedPlace = await _placeService.GetPlaceAsync(
                suggestion.PlaceId,
                session,
                requestCancellation.Token);

            if (version != _originSearchVersion)
            {
                return;
            }

            SetOriginSelection(selectedPlace.Address, selectedPlace.PlaceId);
            _originSession = null;
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception) when (version == _originSearchVersion)
        {
            OriginSearchError = GetPlaceSearchError(exception);
        }
        finally
        {
            if (version == _originSearchVersion)
            {
                IsSearchingOrigin = false;
            }
        }
    }

    [RelayCommand(AllowConcurrentExecutions = false)]
    private async Task SelectDestinationAsync(
        PlaceSuggestion? suggestion,
        CancellationToken cancellationToken)
    {
        if (suggestion is null || _destinationSession is null)
        {
            return;
        }

        var session = _destinationSession;
        var requestCancellation =
            CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var version = ++_destinationSearchVersion;
        ReplaceCancellation(
            ref _destinationSearchCancellation,
            requestCancellation);
        DestinationSuggestions.Clear();
        OnPropertyChanged(nameof(HasDestinationSuggestions));
        DestinationSearchError = string.Empty;
        IsSearchingDestination = true;

        try
        {
            var selectedPlace = await _placeService.GetPlaceAsync(
                suggestion.PlaceId,
                session,
                requestCancellation.Token);

            if (version != _destinationSearchVersion)
            {
                return;
            }

            SetDestinationSelection(selectedPlace.Address, selectedPlace.PlaceId);
            _destinationSession = null;
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception) when (version == _destinationSearchVersion)
        {
            DestinationSearchError = GetPlaceSearchError(exception);
        }
        finally
        {
            if (version == _destinationSearchVersion)
            {
                IsSearchingDestination = false;
            }
        }
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
            var wasEditing = IsEditing;

            if (route.Role == RouteRole.Driver)
            {
                var mapRoute = await _mapRouteService.CalculateAsync(
                    route.OriginPlaceId,
                    route.DestinationPlaceId,
                    cancellationToken);

                if (mapRoute.DistanceMeters <= 0
                    || mapRoute.Duration <= TimeSpan.Zero)
                {
                    throw new InvalidOperationException(
                        "Não foi possível calcular a rota. Tente novamente.");
                }

                route = WithCalculatedDistance(
                    route,
                    mapRoute.DistanceMeters / 1000m);
            }

            if (wasEditing)
            {
                await _routeService.UpdateAsync(route, cancellationToken);
            }
            else
            {
                await _routeService.CreateAsync(route, cancellationToken);
            }

            ResetForm();
            SuccessMessage = wasEditing
                ? "Rota atualizada com sucesso."
                : "Rota cadastrada com sucesso.";
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

        if (string.IsNullOrWhiteSpace(OriginPlaceId))
        {
            SetError("Selecione uma origem válida nas sugestões.");
            return false;
        }

        if (string.IsNullOrWhiteSpace(DestinationPlaceId))
        {
            SetError("Selecione um destino válido nas sugestões.");
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
            Id = _routeBeingEdited?.Id ?? string.Empty,
            Role = SelectedRole.Role,
            Origin = normalizedOrigin,
            OriginPlaceId = OriginPlaceId.Trim(),
            Destination = normalizedDestination,
            DestinationPlaceId = DestinationPlaceId.Trim(),
            DaysOfWeek = selectedDays,
            DepartureTimeMinutes = (int)DepartureTime.TotalMinutes,
            AvailableSeats = SelectedRole.Role == RouteRole.Driver
                ? AvailableSeats
                : null,
            EstimatedDistanceKm = 0m
        };

        return true;
    }

    private void ResetForm()
    {
        ResetPlaceSearches();
        _routeBeingEdited = null;
        IsEditing = false;
        SelectedRole = null;
        SetOriginSelection(string.Empty, string.Empty);
        SetDestinationSelection(string.Empty, string.Empty);
        DepartureTime = DefaultDepartureTime;
        AvailableSeats = null;

        foreach (var day in Days)
        {
            day.IsSelected = false;
        }
    }

    private void QueueOriginSearch(string? value)
    {
        var query = value?.Trim() ?? string.Empty;
        var version = ++_originSearchVersion;
        ReplaceCancellation(ref _originSearchCancellation, null);
        OriginSuggestions.Clear();
        OnPropertyChanged(nameof(HasOriginSuggestions));
        OriginSearchError = string.Empty;
        IsSearchingOrigin = false;

        if (query.Length < MinimumAutocompleteLength)
        {
            _originSession = null;
            return;
        }

        _originSession ??= _placeService.CreateSession();
        _originSearchCancellation = new CancellationTokenSource();
        IsSearchingOrigin = true;
        _ = SearchOriginAsync(
            query,
            _originSession,
            version,
            _originSearchCancellation.Token);
    }

    private void QueueDestinationSearch(string? value)
    {
        var query = value?.Trim() ?? string.Empty;
        var version = ++_destinationSearchVersion;
        ReplaceCancellation(ref _destinationSearchCancellation, null);
        DestinationSuggestions.Clear();
        OnPropertyChanged(nameof(HasDestinationSuggestions));
        DestinationSearchError = string.Empty;
        IsSearchingDestination = false;

        if (query.Length < MinimumAutocompleteLength)
        {
            _destinationSession = null;
            return;
        }

        _destinationSession ??= _placeService.CreateSession();
        _destinationSearchCancellation = new CancellationTokenSource();
        IsSearchingDestination = true;
        _ = SearchDestinationAsync(
            query,
            _destinationSession,
            version,
            _destinationSearchCancellation.Token);
    }

    private async Task SearchOriginAsync(
        string query,
        PlaceAutocompleteSession session,
        int version,
        CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(AutocompleteDebounce, cancellationToken);
            var suggestions = await _placeService.SearchAsync(
                query,
                session,
                cancellationToken);

            if (version != _originSearchVersion)
            {
                return;
            }

            ReplaceSuggestions(OriginSuggestions, suggestions);
            OnPropertyChanged(nameof(HasOriginSuggestions));
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception) when (version == _originSearchVersion)
        {
            OriginSearchError = GetPlaceSearchError(exception);
        }
        finally
        {
            if (version == _originSearchVersion)
            {
                IsSearchingOrigin = false;
            }
        }
    }

    private async Task SearchDestinationAsync(
        string query,
        PlaceAutocompleteSession session,
        int version,
        CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(AutocompleteDebounce, cancellationToken);
            var suggestions = await _placeService.SearchAsync(
                query,
                session,
                cancellationToken);

            if (version != _destinationSearchVersion)
            {
                return;
            }

            ReplaceSuggestions(DestinationSuggestions, suggestions);
            OnPropertyChanged(nameof(HasDestinationSuggestions));
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception) when (version == _destinationSearchVersion)
        {
            DestinationSearchError = GetPlaceSearchError(exception);
        }
        finally
        {
            if (version == _destinationSearchVersion)
            {
                IsSearchingDestination = false;
            }
        }
    }

    private void SetOriginSelection(string address, string placeId)
    {
        _isApplyingOriginSelection = true;

        try
        {
            Origin = address;
            OriginPlaceId = placeId;
        }
        finally
        {
            _isApplyingOriginSelection = false;
        }

        OriginSuggestions.Clear();
        OnPropertyChanged(nameof(HasOriginSuggestions));
        OriginSearchError = string.Empty;
    }

    private void SetDestinationSelection(string address, string placeId)
    {
        _isApplyingDestinationSelection = true;

        try
        {
            Destination = address;
            DestinationPlaceId = placeId;
        }
        finally
        {
            _isApplyingDestinationSelection = false;
        }

        DestinationSuggestions.Clear();
        OnPropertyChanged(nameof(HasDestinationSuggestions));
        DestinationSearchError = string.Empty;
    }

    private void ResetPlaceSearches()
    {
        ++_originSearchVersion;
        ++_destinationSearchVersion;
        ReplaceCancellation(ref _originSearchCancellation, null);
        ReplaceCancellation(ref _destinationSearchCancellation, null);
        _originSession = null;
        _destinationSession = null;
        OriginSuggestions.Clear();
        DestinationSuggestions.Clear();
        OnPropertyChanged(nameof(HasOriginSuggestions));
        OnPropertyChanged(nameof(HasDestinationSuggestions));
        OriginSearchError = string.Empty;
        DestinationSearchError = string.Empty;
        IsSearchingOrigin = false;
        IsSearchingDestination = false;
    }

    private static void ReplaceSuggestions(
        ObservableCollection<PlaceSuggestion> target,
        IEnumerable<PlaceSuggestion> suggestions)
    {
        target.Clear();

        foreach (var suggestion in suggestions)
        {
            target.Add(suggestion);
        }
    }

    private static void ReplaceCancellation(
        ref CancellationTokenSource? target,
        CancellationTokenSource? replacement)
    {
        target?.Cancel();
        target?.Dispose();
        target = replacement;
    }

    private static string GetPlaceSearchError(Exception exception)
    {
        return string.IsNullOrWhiteSpace(exception.Message)
            ? "Não foi possível buscar endereços agora. Tente novamente."
            : exception.Message;
    }

    private static WeeklyRoute WithCalculatedDistance(
        WeeklyRoute route,
        decimal estimatedDistanceKm)
    {
        return new WeeklyRoute
        {
            Id = route.Id,
            UserId = route.UserId,
            UserName = route.UserName,
            Role = route.Role,
            Origin = route.Origin,
            OriginPlaceId = route.OriginPlaceId,
            Destination = route.Destination,
            DestinationPlaceId = route.DestinationPlaceId,
            DaysOfWeek = route.DaysOfWeek,
            DepartureTimeMinutes = route.DepartureTimeMinutes,
            AvailableSeats = route.AvailableSeats,
            EstimatedDistanceKm = estimatedDistanceKm,
            RequestRevision = route.RequestRevision,
            CreatedAtUtc = route.CreatedAtUtc
        };
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
            ? IsEditing
                ? "Não foi possível atualizar a rota. Tente novamente."
                : "Não foi possível cadastrar a rota. Tente novamente."
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
