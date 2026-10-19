using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using UniRota.Models;
using UniRota.Services.Interfaces;

namespace UniRota.Services.Firebase;

public sealed class FirebaseRouteService : IRouteService
{
    private const string CollectionName = "weeklyRoutes";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;
    private readonly FirebaseOptions _options;
    private readonly IAuthService _authService;

    public FirebaseRouteService(
        HttpClient httpClient,
        FirebaseOptions options,
        IAuthService authService)
    {
        _httpClient = httpClient;
        _options = options;
        _authService = authService;
    }

    public async Task<WeeklyRoute> CreateAsync(
        WeeklyRoute route,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(route);
        EnsureFirebaseIsConfigured();

        var user = GetAuthenticatedUser();
        var userId = user.Id;
        var normalizedRoute = NormalizeAndValidateRoute(
            route,
            userId,
            user.Name,
            string.Empty,
            DateTimeOffset.UtcNow);
        var idToken = await _authService.GetValidIdTokenAsync(cancellationToken);
        EnsureCurrentUserHasNotChanged(userId);

        var requestBody = new
        {
            fields = CreateFirestoreFields(normalizedRoute)
        };

        using var request = CreateJsonRequest(
            HttpMethod.Post,
            BuildCollectionUrl(),
            requestBody);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", idToken);

        var response = await SendAsync(request, cancellationToken);
        EnsureSuccess(response);
        EnsureCurrentUserHasNotChanged(userId);

        var document = DeserializeResponse<FirestoreDocumentDto>(response.Content);
        var createdRoute = ConvertDocument(document);

        EnsureRouteBelongsToUser(createdRoute, userId);
        return createdRoute;
    }

    public async Task<WeeklyRoute> UpdateAsync(
        WeeklyRoute route,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(route);
        EnsureFirebaseIsConfigured();

        var routeId = GetRequiredRouteId(route.Id);
        var userId = GetAuthenticatedUserId();
        var idToken = await _authService.GetValidIdTokenAsync(cancellationToken);
        EnsureCurrentUserHasNotChanged(userId);

        var existingDocument = await GetRouteDocumentAsync(
            routeId,
            idToken,
            cancellationToken);
        var existingRoute = ConvertDocument(existingDocument);
        EnsureRouteBelongsToUser(existingRoute, userId);

        var normalizedRoute = NormalizeAndValidateRoute(
            route,
            userId,
            existingRoute.UserName,
            existingRoute.Id,
            existingRoute.CreatedAtUtc,
            existingRoute);

        EnsureCurrentUserHasNotChanged(userId);

        var requestBody = new
        {
            fields = CreateEditableFirestoreFields(normalizedRoute)
        };

        using var request = CreateJsonRequest(
            HttpMethod.Patch,
            BuildUpdateDocumentUrl(
                routeId,
                GetRequiredUpdateTime(existingDocument)),
            requestBody);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", idToken);

        var response = await SendAsync(request, cancellationToken);
        EnsureSuccess(response);
        EnsureCurrentUserHasNotChanged(userId);

        var updatedDocument = DeserializeResponse<FirestoreDocumentDto>(response.Content);
        var updatedRoute = ConvertDocument(updatedDocument);

        EnsureRouteBelongsToUser(updatedRoute, userId);

        if (!string.Equals(updatedRoute.Id, routeId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "O Firebase retornou um identificador inesperado para a rota atualizada.");
        }

        return updatedRoute;
    }

    public async Task DeleteAsync(
        string routeId,
        CancellationToken cancellationToken = default)
    {
        EnsureFirebaseIsConfigured();

        var normalizedRouteId = GetRequiredRouteId(routeId);
        var userId = GetAuthenticatedUserId();
        var idToken = await _authService.GetValidIdTokenAsync(cancellationToken);
        EnsureCurrentUserHasNotChanged(userId);

        var existingDocument = await GetRouteDocumentAsync(
            normalizedRouteId,
            idToken,
            cancellationToken);
        var existingRoute = ConvertDocument(existingDocument);
        EnsureRouteBelongsToUser(existingRoute, userId);
        EnsureCurrentUserHasNotChanged(userId);

        using var request = new HttpRequestMessage(
            HttpMethod.Delete,
            BuildDeleteDocumentUrl(
                normalizedRouteId,
                GetRequiredUpdateTime(existingDocument)));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", idToken);

        var response = await SendAsync(request, cancellationToken);
        EnsureSuccess(response);
        EnsureCurrentUserHasNotChanged(userId);
    }

    public async Task<IReadOnlyList<WeeklyRoute>> GetMyRoutesAsync(
        CancellationToken cancellationToken = default)
    {
        EnsureFirebaseIsConfigured();

        var userId = GetAuthenticatedUserId();
        var idToken = await _authService.GetValidIdTokenAsync(cancellationToken);
        EnsureCurrentUserHasNotChanged(userId);

        var requestBody = new
        {
            structuredQuery = new
            {
                from = new[]
                {
                    new { collectionId = CollectionName }
                },
                where = new
                {
                    fieldFilter = new
                    {
                        field = new { fieldPath = "userId" },
                        op = "EQUAL",
                        value = new { stringValue = userId }
                    }
                }
            }
        };

        var routes = await RunRouteQueryAsync(
            requestBody,
            idToken,
            userId,
            cancellationToken);

        foreach (var route in routes)
        {
            EnsureRouteBelongsToUser(route, userId);
        }

        return routes
            .OrderByDescending(route => route.CreatedAtUtc)
            .ToArray();
    }

    public async Task<IReadOnlyList<WeeklyRoute>> GetDriverRoutesAsync(
        CancellationToken cancellationToken = default)
    {
        EnsureFirebaseIsConfigured();

        var userId = GetAuthenticatedUserId();
        var idToken = await _authService.GetValidIdTokenAsync(cancellationToken);
        EnsureCurrentUserHasNotChanged(userId);

        var requestBody = new
        {
            structuredQuery = new
            {
                from = new[]
                {
                    new { collectionId = CollectionName }
                },
                where = new
                {
                    fieldFilter = new
                    {
                        field = new { fieldPath = "role" },
                        op = "EQUAL",
                        value = new { stringValue = "driver" }
                    }
                }
            }
        };

        return await RunRouteQueryAsync(
            requestBody,
            idToken,
            userId,
            cancellationToken);
    }

    private static WeeklyRoute NormalizeAndValidateRoute(
        WeeklyRoute route,
        string userId,
        string userName,
        string routeId,
        DateTimeOffset createdAtUtc,
        WeeklyRoute? existingRoute = null)
    {
        if (!Enum.IsDefined(typeof(RouteRole), route.Role))
        {
            throw new ArgumentException("Informe um papel válido para a rota.", nameof(route));
        }

        if (existingRoute is not null && route.Role != existingRoute.Role)
        {
            throw new ArgumentException("O papel da rota não pode ser alterado após a criação.", nameof(route));
        }

        var origin = route.Origin?.Trim() ?? string.Empty;
        var originPlaceId = route.OriginPlaceId?.Trim() ?? string.Empty;
        var destination = route.Destination?.Trim() ?? string.Empty;
        var destinationPlaceId = route.DestinationPlaceId?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(origin))
        {
            throw new ArgumentException("Informe a origem da rota.", nameof(route));
        }

        if (string.IsNullOrWhiteSpace(destination))
        {
            throw new ArgumentException("Informe o destino da rota.", nameof(route));
        }

        if (string.Equals(origin, destination, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                "A origem e o destino devem ser diferentes.",
                nameof(route));
        }

        if (route.DaysOfWeek is null || route.DaysOfWeek.Count == 0)
        {
            throw new ArgumentException(
                "Selecione ao menos um dia da semana.",
                nameof(route));
        }

        if (route.DaysOfWeek.Any(
                day => !Enum.IsDefined(typeof(DayOfWeek), day)))
        {
            throw new ArgumentException(
                "A rota contém um dia da semana inválido.",
                nameof(route));
        }

        if (route.DepartureTimeMinutes is < 0 or > 1439)
        {
            throw new ArgumentOutOfRangeException(
                nameof(route),
                "O horário de saída deve estar entre 0 e 1439 minutos.");
        }

        if (existingRoute is null && GetSeatSchemaValidationError(route) is { } seatSchemaError)
        {
            throw new ArgumentException(seatSchemaError, nameof(route));
        }

        if (route.Role == RouteRole.Driver
            && route.EstimatedDistanceKm <= 0m)
        {
            throw new ArgumentException(
                "Uma rota de motorista deve possuir distância estimada maior que zero.",
                nameof(route));
        }

        var daysOfWeek = route.DaysOfWeek
            .Distinct()
            .OrderBy(day => day)
            .ToArray();

        var weeklySeats = existingRoute is null
            ? route.WeeklyAvailableSeats
            : RouteSeatEditingRules.GetUpdatedWeeklySeats(existingRoute, route.SeatCapacity, daysOfWeek);
        var reservations = existingRoute?.OneTimeReservations ?? route.OneTimeReservations;
        if (existingRoute is null && route.WeeklyAcceptedDays.Count != 0)
            throw new ArgumentException("Uma nova rota deve possuir weeklyAcceptedDays vazio.", nameof(route));

        var normalizedRoute = new WeeklyRoute
        {
            Id = routeId,
            UserId = userId,
            UserName = userName,
            Role = route.Role,
            Origin = origin,
            OriginPlaceId = originPlaceId,
            Destination = destination,
            DestinationPlaceId = destinationPlaceId,
            DaysOfWeek = daysOfWeek,
            WeeklyAcceptedDays = (existingRoute?.WeeklyAcceptedDays ?? route.WeeklyAcceptedDays).ToArray(),
            DepartureTimeMinutes = route.DepartureTimeMinutes,
            SeatCapacity = route.SeatCapacity,
            WeeklyAvailableSeats = weeklySeats.ToDictionary(
                entry => entry.Key,
                entry => entry.Value),
            OneTimeReservations = reservations.ToArray(),
            EstimatedDistanceKm = route.Role == RouteRole.Driver
                ? route.EstimatedDistanceKm
                : 0m,
            RequestRevision = existingRoute?.RequestRevision ?? 0,
            CreatedAtUtc = createdAtUtc
        };

        if (GetSeatSchemaValidationError(normalizedRoute) is { } normalizedSchemaError)
        {
            throw new ArgumentException(normalizedSchemaError, nameof(route));
        }

        return normalizedRoute;
    }

    private static string? GetSeatSchemaValidationError(WeeklyRoute route)
    {
        if (route.WeeklyAcceptedDays is null || route.WeeklyAvailableSeats is null || route.OneTimeReservations is null)
        {
            return "Os campos 'weeklyAcceptedDays', 'weeklyAvailableSeats' e 'oneTimeReservations' são obrigatórios.";
        }

        if (route.WeeklyAcceptedDays.Distinct().Count() != route.WeeklyAcceptedDays.Count
            || route.WeeklyAcceptedDays.Any(day => !route.DaysOfWeek.Contains(day))
            || (route.Role == RouteRole.Driver && route.WeeklyAcceptedDays.Count != 0))
            return "weeklyAcceptedDays deve conter dias únicos da rota e permanecer vazio para motorista.";

        if (route.Role == RouteRole.Passenger)
        {
            return route.SeatCapacity is not null
                || route.WeeklyAvailableSeats.Count != 0
                || route.OneTimeReservations.Count != 0
                ? "Passageiro deve possuir 'seatCapacity' null, mapa de vagas e reservas vazios."
                : null;
        }

        if (route.SeatCapacity is null or <= 0)
        {
            return "O campo 'seatCapacity' deve ser maior que zero para motorista.";
        }

        var selectedDays = route.DaysOfWeek.ToHashSet();

        if (route.WeeklyAvailableSeats.Count != selectedDays.Count
            || route.WeeklyAvailableSeats.Keys.Any(day => !selectedDays.Contains(day)))
        {
            return "As chaves de 'weeklyAvailableSeats' devem corresponder aos dias da rota.";
        }

        if (route.WeeklyAvailableSeats.Values.Any(
                seats => seats < 0 || seats > route.SeatCapacity.Value))
        {
            return "Os saldos de 'weeklyAvailableSeats' devem estar entre zero e 'seatCapacity'.";
        }

        var requestIds = new HashSet<string>(StringComparer.Ordinal);

        foreach (var reservation in route.OneTimeReservations)
        {
            if (reservation is null || string.IsNullOrWhiteSpace(reservation.RequestId))
            {
                return "Toda reserva deve possuir um 'requestId' não vazio.";
            }

            if (!requestIds.Add(reservation.RequestId))
            {
                return "O campo 'oneTimeReservations' contém 'requestId' duplicado.";
            }

            if (reservation.DayOfWeek != reservation.Date.DayOfWeek)
            {
                return "O campo 'dayOfWeek' da reserva deve corresponder a 'date'.";
            }

            if (!selectedDays.Contains(reservation.DayOfWeek))
            {
                return "A reserva deve pertencer a um dia existente da rota.";
            }
        }

        return null;
    }

    private static Dictionary<string, object> CreateFirestoreFields(WeeklyRoute route)
    {
        var fields = CreateEditableFirestoreFields(route);
        fields["userId"] = new { stringValue = route.UserId };
        fields["userName"] = new { stringValue = route.UserName };
        fields["requestRevision"] = new
        {
            integerValue = route.RequestRevision.ToString(
                CultureInfo.InvariantCulture)
        };
        fields["createdAtUtc"] = new
        {
            timestampValue = route.CreatedAtUtc.ToUniversalTime().ToString("O")
        };

        return fields;
    }

    private static Dictionary<string, object> CreateEditableFirestoreFields(
        WeeklyRoute route)
    {
        var fields = new Dictionary<string, object>
        {
            ["role"] = new { stringValue = SerializeRole(route.Role) },
            ["origin"] = new { stringValue = route.Origin },
            ["destination"] = new { stringValue = route.Destination },
            ["daysOfWeek"] = new
            {
                arrayValue = new
                {
                    values = route.DaysOfWeek
                        .Select(day => new { stringValue = day.ToString() })
                        .ToArray()
                }
            },
            ["weeklyAcceptedDays"] = new
            {
                arrayValue = new { values = route.WeeklyAcceptedDays.Select(day => new { stringValue = day.ToString() }).ToArray() }
            },
            ["departureTimeMinutes"] = new
            {
                integerValue = route.DepartureTimeMinutes.ToString(
                    CultureInfo.InvariantCulture)
            },
            ["seatCapacity"] = route.SeatCapacity is not null
                ? new
                {
                    integerValue = route.SeatCapacity.Value.ToString(
                        CultureInfo.InvariantCulture)
                }
                : new { nullValue = (object?)null },
            ["weeklyAvailableSeats"] = new
            {
                mapValue = new
                {
                    fields = route.WeeklyAvailableSeats.ToDictionary(
                        entry => entry.Key.ToString(),
                        entry => new
                        {
                            integerValue = entry.Value.ToString(
                                CultureInfo.InvariantCulture)
                        })
                }
            },
            ["oneTimeReservations"] = new
            {
                arrayValue = new
                {
                    values = route.OneTimeReservations.Select(reservation => new
                    {
                        mapValue = new
                        {
                            fields = new Dictionary<string, object>
                            {
                                ["requestId"] = new { stringValue = reservation.RequestId },
                                ["date"] = new
                                {
                                    stringValue = reservation.Date.ToString(
                                        "yyyy-MM-dd",
                                        CultureInfo.InvariantCulture)
                                },
                                ["dayOfWeek"] = new
                                {
                                    stringValue = reservation.DayOfWeek.ToString()
                                }
                            }
                        }
                    }).ToArray()
                }
            },
            ["estimatedDistanceKm"] = new
            {
                doubleValue = (double)route.EstimatedDistanceKm
            }
        };

        AddOptionalStringField(fields, "originPlaceId", route.OriginPlaceId);
        AddOptionalStringField(
            fields,
            "destinationPlaceId",
            route.DestinationPlaceId);

        return fields;
    }

    private static void AddOptionalStringField(
        IDictionary<string, object> fields,
        string fieldName,
        string value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            fields[fieldName] = new { stringValue = value };
        }
    }

    private static WeeklyRoute ConvertDocument(FirestoreDocumentDto document)
    {
        var id = GetDocumentId(document.Name);
        var fields = document.Fields;
        var role = ParseRole(GetRequiredStringField(fields, "role"));
        var daysOfWeek = GetRequiredDaysOfWeekField(fields, "daysOfWeek", id);
        var departureTimeMinutes = GetRequiredInt32Field(
            fields,
            "departureTimeMinutes");

        if (departureTimeMinutes is < 0 or > 1439)
        {
            throw CreateInvalidDocumentException(
                id,
                "o campo 'departureTimeMinutes' está fora do intervalo permitido");
        }

        int? seatCapacity;
        var estimatedDistanceKm = 0m;

        if (role == RouteRole.Driver)
        {
            seatCapacity = GetRequiredInt32Field(fields, "seatCapacity");
            estimatedDistanceKm = GetRequiredPositiveDecimalField(
                fields,
                "estimatedDistanceKm",
                id);
        }
        else
        {
            if (!fields.TryGetValue("seatCapacity", out var capacityField)
                || capacityField?.NullValue.ValueKind != JsonValueKind.Null)
            {
                throw CreateInvalidDocumentException(
                    id,
                    "o campo 'seatCapacity' deve ser explicitamente null para passageiro");
            }

            seatCapacity = null;
        }

        var route = new WeeklyRoute
        {
            Id = id,
            UserId = GetRequiredStringField(fields, "userId"),
            UserName = GetOptionalStringField(fields, "userName"),
            Role = role,
            Origin = GetRequiredStringField(fields, "origin"),
            OriginPlaceId = GetOptionalStringField(fields, "originPlaceId"),
            Destination = GetRequiredStringField(fields, "destination"),
            DestinationPlaceId = GetOptionalStringField(
                fields,
                "destinationPlaceId"),
            DaysOfWeek = daysOfWeek,
            DepartureTimeMinutes = departureTimeMinutes,
            SeatCapacity = seatCapacity,
            WeeklyAcceptedDays = GetRequiredDaysOfWeekField(fields, "weeklyAcceptedDays", id, allowEmpty: true),
            WeeklyAvailableSeats = GetRequiredWeeklyAvailableSeats(fields, id),
            OneTimeReservations = GetRequiredOneTimeReservations(fields, id),
            EstimatedDistanceKm = estimatedDistanceKm,
            RequestRevision = GetOptionalNonNegativeInt64Field(
                fields,
                "requestRevision",
                id),
            CreatedAtUtc = GetRequiredTimestampField(fields, "createdAtUtc")
        };

        var seatSchemaError = GetSeatSchemaValidationError(route);

        if (seatSchemaError is not null)
        {
            throw CreateInvalidDocumentException(id, seatSchemaError);
        }

        return route;
    }

    private static IReadOnlyDictionary<DayOfWeek, int> GetRequiredWeeklyAvailableSeats(
        IReadOnlyDictionary<string, FirestoreValueDto> fields,
        string documentId)
    {
        if (!fields.TryGetValue("weeklyAvailableSeats", out var field)
            || field?.MapValue?.Fields is not { } mapFields)
        {
            throw CreateInvalidDocumentException(
                documentId,
                "o campo 'weeklyAvailableSeats' deve conter um mapValue");
        }

        var seats = new Dictionary<DayOfWeek, int>();

        foreach (var entry in mapFields)
        {
            var day = ParseCanonicalDay(entry.Key, "weeklyAvailableSeats", documentId);
            seats.Add(day, GetRequiredInt32Field(mapFields, entry.Key));
        }

        return seats;
    }

    private static IReadOnlyList<OneTimeReservation> GetRequiredOneTimeReservations(
        IReadOnlyDictionary<string, FirestoreValueDto> fields,
        string documentId)
    {
        if (!fields.TryGetValue("oneTimeReservations", out var field)
            || field?.ArrayValue?.Values is not { } values)
        {
            throw CreateInvalidDocumentException(
                documentId,
                "o campo 'oneTimeReservations' deve conter um arrayValue");
        }

        var reservations = new List<OneTimeReservation>();

        foreach (var value in values)
        {
            if (value?.MapValue?.Fields is not { } reservationFields)
            {
                throw CreateInvalidDocumentException(
                    documentId,
                    "cada entrada de 'oneTimeReservations' deve conter um mapValue");
            }

            var dateText = GetRequiredStringField(reservationFields, "date");

            if (!DateOnly.TryParseExact(
                    dateText,
                    "yyyy-MM-dd",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out var date))
            {
                throw CreateInvalidDocumentException(
                    documentId,
                    "o campo 'date' da reserva deve conter uma data válida em yyyy-MM-dd");
            }

            reservations.Add(new OneTimeReservation
            {
                RequestId = GetRequiredStringField(reservationFields, "requestId"),
                Date = date,
                DayOfWeek = ParseCanonicalDay(
                    GetRequiredStringField(reservationFields, "dayOfWeek"),
                    "oneTimeReservations.dayOfWeek",
                    documentId)
            });
        }

        return reservations;
    }

    private static DayOfWeek ParseCanonicalDay(
        string? value,
        string fieldName,
        string documentId)
    {
        if (!Enum.TryParse<DayOfWeek>(value, out var day)
            || !Enum.IsDefined(typeof(DayOfWeek), day)
            || !string.Equals(value, day.ToString(), StringComparison.Ordinal))
        {
            throw CreateInvalidDocumentException(
                documentId,
                $"o campo '{fieldName}' deve usar nomes canônicos de DayOfWeek");
        }

        return day;
    }

    private async Task<IReadOnlyList<WeeklyRoute>> RunRouteQueryAsync(
        object requestBody,
        string idToken,
        string expectedUserId,
        CancellationToken cancellationToken)
    {
        using var request = CreateJsonRequest(
            HttpMethod.Post,
            BuildRunQueryUrl(),
            requestBody);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", idToken);

        var response = await SendAsync(request, cancellationToken);
        EnsureSuccess(response);
        EnsureCurrentUserHasNotChanged(expectedUserId);

        var queryResults = DeserializeResponse<List<RunQueryResultDto>>(response.Content);

        return queryResults
            .Where(result => result.Document is not null)
            .Select(result => ConvertDocument(result.Document!))
            .ToArray();
    }

    private async Task<FirestoreDocumentDto> GetRouteDocumentAsync(
        string routeId,
        string idToken,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            BuildDocumentUrl(routeId));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", idToken);

        var response = await SendAsync(request, cancellationToken);
        EnsureSuccess(response);

        return DeserializeResponse<FirestoreDocumentDto>(response.Content);
    }

    private string GetAuthenticatedUserId()
    {
        return GetAuthenticatedUser().Id;
    }

    private User GetAuthenticatedUser()
    {
        var user = _authService.CurrentUser
            ?? throw new InvalidOperationException(
                "Não há uma sessão autenticada. Entre novamente para continuar.");

        if (string.IsNullOrWhiteSpace(user.Id))
        {
            throw new InvalidOperationException(
                "A sessão autenticada não contém um identificador de usuário válido.");
        }

        return user;
    }

    private void EnsureCurrentUserHasNotChanged(string expectedUserId)
    {
        if (!string.Equals(
                _authService.CurrentUser?.Id,
                expectedUserId,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "A sessão autenticada foi alterada durante a operação. Tente novamente.");
        }
    }

    private static void EnsureRouteBelongsToUser(
        WeeklyRoute route,
        string expectedUserId)
    {
        if (!string.Equals(route.UserId, expectedUserId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "O Firebase retornou uma rota que não pertence ao usuário autenticado.");
        }
    }

    private static string GetRequiredRouteId(string? routeId)
    {
        var normalizedRouteId = routeId?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(normalizedRouteId))
        {
            throw new ArgumentException(
                "Informe o identificador da rota.",
                nameof(routeId));
        }

        return normalizedRouteId;
    }

    private static string GetRequiredUpdateTime(FirestoreDocumentDto document)
    {
        return !string.IsNullOrWhiteSpace(document.UpdateTime)
            ? document.UpdateTime
            : throw new InvalidOperationException(
                "O Firebase não retornou a versão esperada para a rota.");
    }

    private static string SerializeRole(RouteRole role)
    {
        return role switch
        {
            RouteRole.Driver => "driver",
            RouteRole.Passenger => "passenger",
            _ => throw new ArgumentOutOfRangeException(nameof(role))
        };
    }

    private static RouteRole ParseRole(string value)
    {
        return value.ToLowerInvariant() switch
        {
            "driver" => RouteRole.Driver,
            "passenger" => RouteRole.Passenger,
            _ => throw new InvalidOperationException(
                $"O documento de rota contém o papel inválido '{value}'.")
        };
    }

    private static IReadOnlyList<DayOfWeek> GetRequiredDaysOfWeekField(
        IReadOnlyDictionary<string, FirestoreValueDto> fields,
        string fieldName,
        string documentId,
        bool allowEmpty = false)
    {
        if (!fields.TryGetValue(fieldName, out var field)
            || field?.ArrayValue?.Values is null
            || (!allowEmpty && field.ArrayValue.Values.Count == 0))
        {
            throw new InvalidOperationException(
                $"O documento de rota não contém o campo obrigatório '{fieldName}'.");
        }

        var days = new List<DayOfWeek>();

        foreach (var value in field.ArrayValue.Values)
        {
            days.Add(ParseCanonicalDay(value?.StringValue, fieldName, documentId));
        }

        if (allowEmpty && days.Distinct().Count() != days.Count)
            throw CreateInvalidDocumentException(documentId, $"{fieldName} contém duplicados");

        return days
            .Distinct()
            .OrderBy(day => day)
            .ToArray();
    }

    private static string GetRequiredStringField(
        IReadOnlyDictionary<string, FirestoreValueDto> fields,
        string fieldName)
    {
        if (fields.TryGetValue(fieldName, out var field)
            && !string.IsNullOrWhiteSpace(field?.StringValue))
        {
            return field.StringValue;
        }

        throw new InvalidOperationException(
            $"O documento de rota não contém o campo obrigatório '{fieldName}'.");
    }

    private static string GetOptionalStringField(
        IReadOnlyDictionary<string, FirestoreValueDto> fields,
        string fieldName)
    {
        return fields.TryGetValue(fieldName, out var field)
            ? field.StringValue ?? string.Empty
            : string.Empty;
    }

    private static int GetRequiredInt32Field(
        IReadOnlyDictionary<string, FirestoreValueDto> fields,
        string fieldName)
    {
        if (!fields.TryGetValue(fieldName, out var field) || field is null)
        {
            throw new InvalidOperationException(
                $"O documento de rota não contém o campo obrigatório '{fieldName}'.");
        }

        var integerValue = field.IntegerValue;
        long parsedValue;

        if (integerValue.ValueKind == JsonValueKind.String)
        {
            if (!long.TryParse(
                    integerValue.GetString(),
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out parsedValue))
            {
                throw new InvalidOperationException(
                    $"O documento de rota contém um inteiro inválido em '{fieldName}'.");
            }
        }
        else if (integerValue.ValueKind == JsonValueKind.Number
                 && integerValue.TryGetInt64(out parsedValue))
        {
        }
        else
        {
            throw new InvalidOperationException(
                $"O documento de rota não contém o campo inteiro obrigatório '{fieldName}'.");
        }

        if (parsedValue is < int.MinValue or > int.MaxValue)
        {
            throw new InvalidOperationException(
                $"O valor de '{fieldName}' excede o intervalo suportado pelo aplicativo.");
        }

        return (int)parsedValue;
    }

    private static decimal GetRequiredPositiveDecimalField(
        IReadOnlyDictionary<string, FirestoreValueDto> fields,
        string fieldName,
        string documentId)
    {
        if (!fields.TryGetValue(fieldName, out var field)
            || field.DoubleValue.ValueKind != JsonValueKind.Number
            || !field.DoubleValue.TryGetDecimal(out var value))
        {
            throw CreateInvalidDocumentException(
                documentId,
                $"o campo '{fieldName}' não contém um número decimal válido");
        }

        if (value <= 0m)
        {
            throw CreateInvalidDocumentException(
                documentId,
                $"o campo '{fieldName}' deve ser maior que zero para motorista");
        }

        return value;
    }

    private static long GetOptionalNonNegativeInt64Field(
        IReadOnlyDictionary<string, FirestoreValueDto> fields,
        string fieldName,
        string documentId)
    {
        if (!fields.TryGetValue(fieldName, out var field))
        {
            return 0;
        }

        var integerValue = field.IntegerValue;
        long parsedValue;

        if (integerValue.ValueKind == JsonValueKind.String)
        {
            if (!long.TryParse(
                    integerValue.GetString(),
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out parsedValue))
            {
                throw CreateInvalidDocumentException(
                    documentId,
                    $"o campo '{fieldName}' contém um inteiro inválido");
            }
        }
        else if (integerValue.ValueKind == JsonValueKind.Number
                 && integerValue.TryGetInt64(out parsedValue))
        {
        }
        else
        {
            throw CreateInvalidDocumentException(
                documentId,
                $"o campo '{fieldName}' não contém um inteiro válido");
        }

        if (parsedValue < 0)
        {
            throw CreateInvalidDocumentException(
                documentId,
                $"o campo '{fieldName}' não pode ser negativo");
        }

        return parsedValue;
    }

    private static DateTimeOffset GetRequiredTimestampField(
        IReadOnlyDictionary<string, FirestoreValueDto> fields,
        string fieldName)
    {
        if (fields.TryGetValue(fieldName, out var field)
            && field.TimestampValue is not null)
        {
            return field.TimestampValue.Value.ToUniversalTime();
        }

        throw new InvalidOperationException(
            $"O documento de rota não contém o campo obrigatório '{fieldName}'.");
    }

    private static string GetDocumentId(string? documentName)
    {
        if (!string.IsNullOrWhiteSpace(documentName))
        {
            var separatorIndex = documentName.LastIndexOf('/');

            if (separatorIndex >= 0 && separatorIndex < documentName.Length - 1)
            {
                return Uri.UnescapeDataString(documentName[(separatorIndex + 1)..]);
            }
        }

        throw new InvalidOperationException(
            "O Firebase não retornou o identificador esperado para a rota.");
    }

    private static InvalidOperationException CreateInvalidDocumentException(
        string documentId,
        string reason)
    {
        return new InvalidOperationException(
            $"O documento de rota '{documentId}' é inválido: {reason}.");
    }

    private async Task<FirebaseHttpResponse> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        try
        {
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            return new FirebaseHttpResponse(response.IsSuccessStatusCode, content);
        }
        catch (HttpRequestException exception)
        {
            throw new InvalidOperationException(
                "Não foi possível conectar aos serviços do Firebase. Verifique sua conexão.",
                exception);
        }
    }

    private static HttpRequestMessage CreateJsonRequest(
        HttpMethod method,
        string url,
        object requestBody)
    {
        return new HttpRequestMessage(method, url)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(requestBody, JsonOptions),
                Encoding.UTF8,
                "application/json")
        };
    }

    private static void EnsureSuccess(FirebaseHttpResponse response)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var firebaseCode = ExtractFirebaseErrorCode(response.Content);
        var message = firebaseCode switch
        {
            "PERMISSION_DENIED" =>
                "O Firebase negou acesso às rotas semanais.",
            "UNAUTHENTICATED" =>
                "Sua sessão não é válida. Entre novamente para continuar.",
            "NOT_FOUND" =>
                "A rota semanal não foi encontrada no Firebase.",
            "FAILED_PRECONDITION" =>
                "A rota foi alterada por outra operação. Recarregue a lista e tente novamente.",
            _ => "Não foi possível concluir a operação de rotas no Firebase."
        };

        var exception = new InvalidOperationException(message);
        exception.Data["FirebaseCode"] = firebaseCode;
        throw exception;
    }

    private static string ExtractFirebaseErrorCode(string content)
    {
        try
        {
            using var document = JsonDocument.Parse(content);

            if (document.RootElement.TryGetProperty("error", out var error)
                && error.TryGetProperty("status", out var status)
                && !string.IsNullOrWhiteSpace(status.GetString()))
            {
                return status.GetString()!;
            }

            if (document.RootElement.TryGetProperty("error", out error)
                && error.TryGetProperty("message", out var message)
                && !string.IsNullOrWhiteSpace(message.GetString()))
            {
                return message.GetString()!.Split(':', 2)[0].Trim();
            }
        }
        catch (JsonException)
        {
        }

        return "UNKNOWN_ERROR";
    }

    private static T DeserializeResponse<T>(string content)
    {
        try
        {
            return JsonSerializer.Deserialize<T>(content, JsonOptions)
                ?? throw new JsonException("A resposta estava vazia.");
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException(
                "O Firebase retornou uma resposta em formato inesperado.",
                exception);
        }
    }

    private string BuildCollectionUrl()
    {
        return BuildDocumentsBaseUrl() + $"/{CollectionName}";
    }

    private string BuildRunQueryUrl()
    {
        return BuildDocumentsBaseUrl() + ":runQuery";
    }

    private string BuildDocumentUrl(string routeId)
    {
        return BuildCollectionUrl() + $"/{Escape(routeId)}";
    }

    private string BuildUpdateDocumentUrl(string routeId, string updateTime)
    {
        var editableFields = new[]
        {
            "role",
            "origin",
            "originPlaceId",
            "destination",
            "destinationPlaceId",
            "daysOfWeek",
            "departureTimeMinutes",
            "seatCapacity",
            "weeklyAvailableSeats",
            "oneTimeReservations",
            "weeklyAcceptedDays",
            "estimatedDistanceKm"
        };
        var updateMask = string.Join(
            "&",
            editableFields.Select(
                field => $"updateMask.fieldPaths={Escape(field)}"));

        return $"{BuildDocumentUrl(routeId)}?{updateMask}" +
               "&" + BuildUpdateVersionPrecondition(updateTime);
    }

    private string BuildUpdateVersionPrecondition(string updateTime)
    {
        if (!_options.UseEmulators)
        {
            return $"currentDocument.updateTime={Escape(updateTime)}";
        }

        // O emulador interpreta o Timestamp RFC3339 na query do PATCH como
        // versão zero. Seus campos protobuf preservam a mesma precondição.
        if (!DateTimeOffset.TryParse(
                updateTime,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var timestamp))
        {
            throw new InvalidOperationException("O Firebase retornou uma versão de rota inválida.");
        }

        var seconds = timestamp.ToUnixTimeSeconds();
        var nanos = (timestamp.Ticks % TimeSpan.TicksPerSecond) * 100;
        return "currentDocument.updateTime.seconds=" + seconds.ToString(CultureInfo.InvariantCulture)
            + "&currentDocument.updateTime.nanos=" + nanos.ToString(CultureInfo.InvariantCulture);
    }

    private string BuildDeleteDocumentUrl(string routeId, string updateTime)
    {
        return $"{BuildDocumentUrl(routeId)}" +
               $"?currentDocument.updateTime={Escape(updateTime)}";
    }

    private string BuildDocumentsBaseUrl()
    {
        return $"{_options.FirestoreBaseUrl}/projects/{Escape(_options.ProjectId)}" +
               "/databases/(default)/documents";
    }

    private void EnsureFirebaseIsConfigured()
    {
        if (string.IsNullOrWhiteSpace(_options.ProjectId))
        {
            throw new InvalidOperationException(
                "Configure FirebaseOptions.ProjectId em MauiProgram.cs.");
        }
    }

    private static string Escape(string value) => Uri.EscapeDataString(value);

    private sealed record FirebaseHttpResponse(bool IsSuccessStatusCode, string Content);

    private sealed class RunQueryResultDto
    {
        [JsonPropertyName("document")]
        public FirestoreDocumentDto? Document { get; init; }
    }

    private sealed class FirestoreDocumentDto
    {
        [JsonPropertyName("name")]
        public string? Name { get; init; }

        [JsonPropertyName("fields")]
        public Dictionary<string, FirestoreValueDto> Fields { get; init; } = [];

        [JsonPropertyName("updateTime")]
        public string? UpdateTime { get; init; }
    }

    private sealed class FirestoreValueDto
    {
        [JsonPropertyName("stringValue")]
        public string? StringValue { get; init; }

        [JsonPropertyName("integerValue")]
        public JsonElement IntegerValue { get; init; }

        [JsonPropertyName("nullValue")]
        public JsonElement NullValue { get; init; }

        [JsonPropertyName("doubleValue")]
        public JsonElement DoubleValue { get; init; }

        [JsonPropertyName("timestampValue")]
        public DateTimeOffset? TimestampValue { get; init; }

        [JsonPropertyName("arrayValue")]
        public FirestoreArrayValueDto? ArrayValue { get; init; }

        [JsonPropertyName("mapValue")]
        public FirestoreMapValueDto? MapValue { get; init; }
    }

    private sealed class FirestoreMapValueDto
    {
        [JsonPropertyName("fields")]
        public Dictionary<string, FirestoreValueDto> Fields { get; init; } = [];
    }

    private sealed class FirestoreArrayValueDto
    {
        [JsonPropertyName("values")]
        public List<FirestoreValueDto> Values { get; init; } = [];
    }
}
