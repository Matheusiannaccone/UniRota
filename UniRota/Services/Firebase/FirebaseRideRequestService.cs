using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using UniRota.Models;
using UniRota.Services.Interfaces;

namespace UniRota.Services.Firebase;

public sealed class FirebaseRideRequestService : IRideRequestService
{
    private const string FirestoreBaseUrl = "https://firestore.googleapis.com/v1";
    private const string CollectionName = "rideRequests";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;
    private readonly FirebaseOptions _options;
    private readonly IAuthService _authService;
    private readonly IRouteService _routeService;
    private readonly SemaphoreSlim _createLock = new(1, 1);

    public FirebaseRideRequestService(
        HttpClient httpClient,
        FirebaseOptions options,
        IAuthService authService,
        IRouteService routeService)
    {
        _httpClient = httpClient;
        _options = options;
        _authService = authService;
        _routeService = routeService;
    }

    public async Task<RideRequest> CreateAsync(
        string passengerRouteId,
        MatchResult match,
        RideRequestType type,
        DateOnly? requestedDate,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(match);
        EnsureFirebaseIsConfigured();

        var user = GetAuthenticatedUser();
        var userId = user.Id;
        var normalizedPassengerRouteId = GetRequiredRouteId(
            passengerRouteId,
            nameof(passengerRouteId));
        var driverRoute = match.DriverRoute
            ?? throw new ArgumentException(
                "O resultado selecionado não contém uma rota de motorista.",
                nameof(match));
        var normalizedDriverRouteId = GetRequiredRouteId(
            driverRoute.Id,
            nameof(match));

        ValidateDriverRoute(driverRoute, userId);

        var compatibleDays = RideRequestRules.ValidateForCreation(
            type,
            match.CompatibleDays,
            requestedDate,
            DateOnly.FromDateTime(DateTime.Today));

        await _createLock.WaitAsync(cancellationToken);

        try
        {
            await EnsurePassengerRouteBelongsToUserAsync(
                normalizedPassengerRouteId,
                userId,
                cancellationToken);
            EnsureCurrentUserHasNotChanged(userId);

            var idToken = await _authService.GetValidIdTokenAsync(cancellationToken);
            EnsureCurrentUserHasNotChanged(userId);

            var pendingRequests = await GetMyPendingRequestsCoreAsync(
                userId,
                idToken,
                cancellationToken);

            if (pendingRequests.Any(
                    request => IsSameRoutePair(
                        request,
                        normalizedPassengerRouteId,
                        normalizedDriverRouteId)))
            {
                throw new InvalidOperationException(
                    "Já existe uma solicitação pendente para estas rotas.");
            }

            var requestToCreate = new RideRequest
            {
                PassengerUserId = userId,
                PassengerUserName = user.Name,
                DriverUserId = driverRoute.UserId,
                DriverUserName = driverRoute.UserName?.Trim() ?? string.Empty,
                PassengerRouteId = normalizedPassengerRouteId,
                DriverRouteId = normalizedDriverRouteId,
                CompatibleDays = compatibleDays,
                Type = type,
                Status = RideRequestStatus.Pending,
                RequestedDate = requestedDate,
                CreatedAtUtc = DateTimeOffset.UtcNow
            };

            var requestBody = new
            {
                fields = CreateFirestoreFields(requestToCreate)
            };

            using var httpRequest = CreateJsonRequest(
                HttpMethod.Post,
                BuildCollectionUrl(),
                requestBody);
            httpRequest.Headers.Authorization =
                new AuthenticationHeaderValue("Bearer", idToken);

            var response = await SendAsync(httpRequest, cancellationToken);
            EnsureSuccess(response);
            EnsureCurrentUserHasNotChanged(userId);

            var document = DeserializeResponse<FirestoreDocumentDto>(response.Content);
            var createdRequest = ConvertDocument(document);
            EnsureCreatedRequestMatches(
                createdRequest,
                requestToCreate,
                userId);

            return createdRequest;
        }
        finally
        {
            _createLock.Release();
        }
    }

    public async Task<bool> HasPendingRequestAsync(
        string passengerRouteId,
        string driverRouteId,
        CancellationToken cancellationToken = default)
    {
        EnsureFirebaseIsConfigured();

        var normalizedPassengerRouteId = GetRequiredRouteId(
            passengerRouteId,
            nameof(passengerRouteId));
        var normalizedDriverRouteId = GetRequiredRouteId(
            driverRouteId,
            nameof(driverRouteId));
        var userId = GetAuthenticatedUser().Id;
        var idToken = await _authService.GetValidIdTokenAsync(cancellationToken);
        EnsureCurrentUserHasNotChanged(userId);

        var pendingRequests = await GetMyPendingRequestsCoreAsync(
            userId,
            idToken,
            cancellationToken);

        return pendingRequests.Any(
            request => IsSameRoutePair(
                request,
                normalizedPassengerRouteId,
                normalizedDriverRouteId));
    }

    public async Task<IReadOnlyList<RideRequest>> GetMyPendingRequestsAsync(
        CancellationToken cancellationToken = default)
    {
        EnsureFirebaseIsConfigured();

        var userId = GetAuthenticatedUser().Id;
        var idToken = await _authService.GetValidIdTokenAsync(cancellationToken);
        EnsureCurrentUserHasNotChanged(userId);

        return await GetMyPendingRequestsCoreAsync(
            userId,
            idToken,
            cancellationToken);
    }

    private async Task<IReadOnlyList<RideRequest>> GetMyPendingRequestsCoreAsync(
        string userId,
        string idToken,
        CancellationToken cancellationToken)
    {
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
                    compositeFilter = new
                    {
                        op = "AND",
                        filters = new object[]
                        {
                            new
                            {
                                fieldFilter = new
                                {
                                    field = new { fieldPath = "passengerUserId" },
                                    op = "EQUAL",
                                    value = new { stringValue = userId }
                                }
                            },
                            new
                            {
                                fieldFilter = new
                                {
                                    field = new { fieldPath = "status" },
                                    op = "EQUAL",
                                    value = new { stringValue = "pending" }
                                }
                            }
                        }
                    }
                }
            }
        };

        using var request = CreateJsonRequest(
            HttpMethod.Post,
            BuildRunQueryUrl(),
            requestBody);
        request.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", idToken);

        var response = await SendAsync(request, cancellationToken);
        EnsureSuccess(response);
        EnsureCurrentUserHasNotChanged(userId);

        var queryResults = DeserializeResponse<List<RunQueryResultDto>>(
            response.Content);
        var requests = queryResults
            .Where(result => result.Document is not null)
            .Select(result => ConvertDocument(result.Document!))
            .ToArray();

        foreach (var rideRequest in requests)
        {
            EnsurePendingRequestBelongsToUser(rideRequest, userId);
        }

        return requests
            .OrderByDescending(request => request.CreatedAtUtc)
            .ToArray();
    }

    private async Task EnsurePassengerRouteBelongsToUserAsync(
        string passengerRouteId,
        string userId,
        CancellationToken cancellationToken)
    {
        var routes = await _routeService.GetMyRoutesAsync(cancellationToken);
        var passengerRoute = routes.FirstOrDefault(
            route => string.Equals(
                route.Id,
                passengerRouteId,
                StringComparison.Ordinal));

        if (passengerRoute is null
            || !string.Equals(
                passengerRoute.UserId,
                userId,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "A rota de passageiro selecionada não pertence ao usuário autenticado.");
        }

        if (passengerRoute.Role != RouteRole.Passenger)
        {
            throw new InvalidOperationException(
                "A solicitação deve partir de uma rota de passageiro.");
        }
    }

    private static void ValidateDriverRoute(
        WeeklyRoute driverRoute,
        string passengerUserId)
    {
        if (driverRoute.Role != RouteRole.Driver)
        {
            throw new ArgumentException(
                "A solicitação deve ser direcionada a uma rota de motorista.",
                nameof(driverRoute));
        }

        if (string.IsNullOrWhiteSpace(driverRoute.UserId))
        {
            throw new ArgumentException(
                "A rota de motorista não possui um usuário válido.",
                nameof(driverRoute));
        }

        if (string.Equals(
                driverRoute.UserId,
                passengerUserId,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Não é possível solicitar carona para uma rota do próprio usuário.");
        }
    }

    private static bool IsSameRoutePair(
        RideRequest request,
        string passengerRouteId,
        string driverRouteId)
    {
        return string.Equals(
                   request.PassengerRouteId,
                   passengerRouteId,
                   StringComparison.Ordinal)
               && string.Equals(
                   request.DriverRouteId,
                   driverRouteId,
                   StringComparison.Ordinal);
    }

    private static Dictionary<string, object> CreateFirestoreFields(
        RideRequest request)
    {
        return new Dictionary<string, object>
        {
            ["passengerUserId"] = new { stringValue = request.PassengerUserId },
            ["passengerUserName"] = new { stringValue = request.PassengerUserName },
            ["driverUserId"] = new { stringValue = request.DriverUserId },
            ["driverUserName"] = new { stringValue = request.DriverUserName },
            ["passengerRouteId"] = new { stringValue = request.PassengerRouteId },
            ["driverRouteId"] = new { stringValue = request.DriverRouteId },
            ["compatibleDays"] = new
            {
                arrayValue = new
                {
                    values = request.CompatibleDays
                        .Select(day => new { stringValue = day.ToString() })
                        .ToArray()
                }
            },
            ["type"] = new { stringValue = SerializeType(request.Type) },
            ["status"] = new { stringValue = SerializeStatus(request.Status) },
            ["requestedDate"] = request.RequestedDate is not null
                ? new
                {
                    stringValue = request.RequestedDate.Value.ToString(
                        "yyyy-MM-dd",
                        CultureInfo.InvariantCulture)
                }
                : new { nullValue = (object?)null },
            ["createdAtUtc"] = new
            {
                timestampValue = request.CreatedAtUtc
                    .ToUniversalTime()
                    .ToString("O")
            }
        };
    }

    private static RideRequest ConvertDocument(FirestoreDocumentDto document)
    {
        var id = GetDocumentId(document.Name);
        var fields = document.Fields;
        var compatibleDays = GetRequiredDaysOfWeekField(
            fields,
            "compatibleDays",
            id);
        var type = ParseType(GetRequiredStringField(fields, "type", id), id);
        var requestedDate = GetOptionalDateField(fields, "requestedDate", id);

        ValidatePersistedTypeAndDate(
            type,
            requestedDate,
            compatibleDays,
            id);

        return new RideRequest
        {
            Id = id,
            PassengerUserId = GetRequiredStringField(
                fields,
                "passengerUserId",
                id),
            PassengerUserName = GetOptionalStringField(
                fields,
                "passengerUserName"),
            DriverUserId = GetRequiredStringField(fields, "driverUserId", id),
            DriverUserName = GetOptionalStringField(fields, "driverUserName"),
            PassengerRouteId = GetRequiredStringField(
                fields,
                "passengerRouteId",
                id),
            DriverRouteId = GetRequiredStringField(fields, "driverRouteId", id),
            CompatibleDays = compatibleDays,
            Type = type,
            Status = ParseStatus(
                GetRequiredStringField(fields, "status", id),
                id),
            RequestedDate = requestedDate,
            CreatedAtUtc = GetRequiredTimestampField(
                fields,
                "createdAtUtc",
                id)
        };
    }

    private static void ValidatePersistedTypeAndDate(
        RideRequestType type,
        DateOnly? requestedDate,
        IReadOnlyList<DayOfWeek> compatibleDays,
        string documentId)
    {
        if (type == RideRequestType.Once
            && (requestedDate is null
                || !compatibleDays.Contains(requestedDate.Value.DayOfWeek)))
        {
            throw CreateInvalidDocumentException(
                documentId,
                "a data única não corresponde aos dias compatíveis");
        }

        if (type == RideRequestType.Weekly && requestedDate is not null)
        {
            throw CreateInvalidDocumentException(
                documentId,
                "uma solicitação semanal não deve possuir 'requestedDate'");
        }
    }

    private static IReadOnlyList<DayOfWeek> GetRequiredDaysOfWeekField(
        IReadOnlyDictionary<string, FirestoreValueDto> fields,
        string fieldName,
        string documentId)
    {
        if (!fields.TryGetValue(fieldName, out var field)
            || field.ArrayValue is null
            || field.ArrayValue.Values.Count == 0)
        {
            throw CreateInvalidDocumentException(
                documentId,
                $"o campo obrigatório '{fieldName}' está ausente");
        }

        var days = new List<DayOfWeek>();

        foreach (var value in field.ArrayValue.Values)
        {
            if (string.IsNullOrWhiteSpace(value.StringValue)
                || !Enum.TryParse<DayOfWeek>(
                    value.StringValue,
                    ignoreCase: true,
                    out var day)
                || !Enum.IsDefined(typeof(DayOfWeek), day))
            {
                throw CreateInvalidDocumentException(
                    documentId,
                    $"o campo '{fieldName}' contém um dia inválido");
            }

            days.Add(day);
        }

        return days
            .Distinct()
            .OrderBy(day => day)
            .ToArray();
    }

    private static string GetRequiredStringField(
        IReadOnlyDictionary<string, FirestoreValueDto> fields,
        string fieldName,
        string documentId)
    {
        if (fields.TryGetValue(fieldName, out var field)
            && !string.IsNullOrWhiteSpace(field.StringValue))
        {
            return field.StringValue;
        }

        throw CreateInvalidDocumentException(
            documentId,
            $"o campo obrigatório '{fieldName}' está ausente");
    }

    private static string GetOptionalStringField(
        IReadOnlyDictionary<string, FirestoreValueDto> fields,
        string fieldName)
    {
        return fields.TryGetValue(fieldName, out var field)
            ? field.StringValue ?? string.Empty
            : string.Empty;
    }

    private static DateOnly? GetOptionalDateField(
        IReadOnlyDictionary<string, FirestoreValueDto> fields,
        string fieldName,
        string documentId)
    {
        if (!fields.TryGetValue(fieldName, out var field)
            || field.StringValue is null)
        {
            return null;
        }

        if (DateOnly.TryParseExact(
                field.StringValue,
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var date))
        {
            return date;
        }

        throw CreateInvalidDocumentException(
            documentId,
            $"o campo '{fieldName}' não contém uma data válida");
    }

    private static DateTimeOffset GetRequiredTimestampField(
        IReadOnlyDictionary<string, FirestoreValueDto> fields,
        string fieldName,
        string documentId)
    {
        if (fields.TryGetValue(fieldName, out var field)
            && field.TimestampValue is not null)
        {
            return field.TimestampValue.Value.ToUniversalTime();
        }

        throw CreateInvalidDocumentException(
            documentId,
            $"o campo obrigatório '{fieldName}' está ausente");
    }

    private static string SerializeType(RideRequestType type)
    {
        return type switch
        {
            RideRequestType.Once => "once",
            RideRequestType.Weekly => "weekly",
            _ => throw new ArgumentOutOfRangeException(nameof(type))
        };
    }

    private static RideRequestType ParseType(string value, string documentId)
    {
        return value.ToLowerInvariant() switch
        {
            "once" => RideRequestType.Once,
            "weekly" => RideRequestType.Weekly,
            _ => throw CreateInvalidDocumentException(
                documentId,
                $"o campo 'type' contém o valor inválido '{value}'")
        };
    }

    private static string SerializeStatus(RideRequestStatus status)
    {
        return status switch
        {
            RideRequestStatus.Pending => "pending",
            RideRequestStatus.Accepted => "accepted",
            RideRequestStatus.Rejected => "rejected",
            _ => throw new ArgumentOutOfRangeException(nameof(status))
        };
    }

    private static RideRequestStatus ParseStatus(
        string value,
        string documentId)
    {
        return value.ToLowerInvariant() switch
        {
            "pending" => RideRequestStatus.Pending,
            "accepted" => RideRequestStatus.Accepted,
            "rejected" => RideRequestStatus.Rejected,
            _ => throw CreateInvalidDocumentException(
                documentId,
                $"o campo 'status' contém o valor inválido '{value}'")
        };
    }

    private static void EnsureCreatedRequestMatches(
        RideRequest createdRequest,
        RideRequest expectedRequest,
        string expectedUserId)
    {
        if (!string.Equals(
                createdRequest.PassengerUserId,
                expectedUserId,
                StringComparison.Ordinal)
            || !string.Equals(
                createdRequest.PassengerRouteId,
                expectedRequest.PassengerRouteId,
                StringComparison.Ordinal)
            || !string.Equals(
                createdRequest.DriverRouteId,
                expectedRequest.DriverRouteId,
                StringComparison.Ordinal)
            || createdRequest.Status != RideRequestStatus.Pending)
        {
            throw new InvalidOperationException(
                "O Firebase retornou dados inesperados para a solicitação criada.");
        }
    }

    private static void EnsurePendingRequestBelongsToUser(
        RideRequest request,
        string expectedUserId)
    {
        if (!string.Equals(
                request.PassengerUserId,
                expectedUserId,
                StringComparison.Ordinal)
            || request.Status != RideRequestStatus.Pending)
        {
            throw new InvalidOperationException(
                "O Firebase retornou uma solicitação que não pertence ao passageiro autenticado.");
        }
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

    private static string GetRequiredRouteId(
        string? routeId,
        string parameterName)
    {
        var normalizedRouteId = routeId?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(normalizedRouteId))
        {
            throw new ArgumentException(
                "Informe um identificador de rota válido.",
                parameterName);
        }

        return normalizedRouteId;
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
            "O Firebase não retornou o identificador esperado para a solicitação.");
    }

    private static InvalidOperationException CreateInvalidDocumentException(
        string documentId,
        string reason)
    {
        return new InvalidOperationException(
            $"O documento de solicitação '{documentId}' é inválido: {reason}.");
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
                "O Firebase negou acesso às solicitações de carona.",
            "UNAUTHENTICATED" =>
                "Sua sessão não é válida. Entre novamente para continuar.",
            "FAILED_PRECONDITION" =>
                "A consulta de solicitações requer configuração adicional no Firebase.",
            _ => "Não foi possível concluir a operação de solicitação no Firebase."
        };

        var exception = new InvalidOperationException(message);
        exception.Data["FirebaseCode"] = firebaseCode;
        exception.Data["FirebaseResponse"] = response.Content;
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

    private string BuildDocumentsBaseUrl()
    {
        return $"{FirestoreBaseUrl}/projects/{Escape(_options.ProjectId)}"
               + "/databases/(default)/documents";
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

    private sealed record FirebaseHttpResponse(
        bool IsSuccessStatusCode,
        string Content);

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
    }

    private sealed class FirestoreValueDto
    {
        [JsonPropertyName("stringValue")]
        public string? StringValue { get; init; }

        [JsonPropertyName("timestampValue")]
        public DateTimeOffset? TimestampValue { get; init; }

        [JsonPropertyName("arrayValue")]
        public FirestoreArrayValueDto? ArrayValue { get; init; }
    }

    private sealed class FirestoreArrayValueDto
    {
        [JsonPropertyName("values")]
        public List<FirestoreValueDto> Values { get; init; } = [];
    }
}
