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
    private const string FirestoreBaseUrl = "https://firestore.googleapis.com/v1";
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

        var userId = GetAuthenticatedUserId();
        var normalizedRoute = NormalizeAndValidateRoute(route, userId);
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

        using var request = CreateJsonRequest(
            HttpMethod.Post,
            BuildRunQueryUrl(),
            requestBody);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", idToken);

        var response = await SendAsync(request, cancellationToken);
        EnsureSuccess(response);
        EnsureCurrentUserHasNotChanged(userId);

        var queryResults = DeserializeResponse<List<RunQueryResultDto>>(response.Content);
        var routes = new List<WeeklyRoute>();

        foreach (var queryResult in queryResults)
        {
            if (queryResult.Document is null)
            {
                continue;
            }

            var route = ConvertDocument(queryResult.Document);
            EnsureRouteBelongsToUser(route, userId);
            routes.Add(route);
        }

        return routes
            .OrderByDescending(route => route.CreatedAtUtc)
            .ToArray();
    }

    private static WeeklyRoute NormalizeAndValidateRoute(
        WeeklyRoute route,
        string userId)
    {
        if (!Enum.IsDefined(typeof(RouteRole), route.Role))
        {
            throw new ArgumentException("Informe um papel válido para a rota.", nameof(route));
        }

        var origin = route.Origin?.Trim() ?? string.Empty;
        var destination = route.Destination?.Trim() ?? string.Empty;

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

        if (route.Role == RouteRole.Driver
            && route.AvailableSeats is null or <= 0)
        {
            throw new ArgumentException(
                "Uma rota de motorista deve oferecer ao menos uma vaga.",
                nameof(route));
        }

        var daysOfWeek = route.DaysOfWeek
            .Distinct()
            .OrderBy(day => day)
            .ToArray();

        return new WeeklyRoute
        {
            UserId = userId,
            Role = route.Role,
            Origin = origin,
            Destination = destination,
            DaysOfWeek = daysOfWeek,
            DepartureTimeMinutes = route.DepartureTimeMinutes,
            AvailableSeats = route.Role == RouteRole.Driver
                ? route.AvailableSeats
                : null,
            CreatedAtUtc = DateTimeOffset.UtcNow
        };
    }

    private static Dictionary<string, object> CreateFirestoreFields(WeeklyRoute route)
    {
        return new Dictionary<string, object>
        {
            ["userId"] = new { stringValue = route.UserId },
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
            ["departureTimeMinutes"] = new
            {
                integerValue = route.DepartureTimeMinutes.ToString(
                    CultureInfo.InvariantCulture)
            },
            ["availableSeats"] = route.AvailableSeats is not null
                ? new
                {
                    integerValue = route.AvailableSeats.Value.ToString(
                        CultureInfo.InvariantCulture)
                }
                : new { nullValue = (object?)null },
            ["createdAtUtc"] = new
            {
                timestampValue = route.CreatedAtUtc.ToUniversalTime().ToString("O")
            }
        };
    }

    private static WeeklyRoute ConvertDocument(FirestoreDocumentDto document)
    {
        var id = GetDocumentId(document.Name);
        var fields = document.Fields;
        var role = ParseRole(GetRequiredStringField(fields, "role"));
        var daysOfWeek = GetRequiredDaysOfWeekField(fields, "daysOfWeek");
        var departureTimeMinutes = GetRequiredInt32Field(
            fields,
            "departureTimeMinutes");

        if (departureTimeMinutes is < 0 or > 1439)
        {
            throw CreateInvalidDocumentException(
                id,
                "o campo 'departureTimeMinutes' está fora do intervalo permitido");
        }

        int? availableSeats = null;

        if (role == RouteRole.Driver)
        {
            availableSeats = GetRequiredInt32Field(fields, "availableSeats");

            if (availableSeats <= 0)
            {
                throw CreateInvalidDocumentException(
                    id,
                    "o campo 'availableSeats' deve ser maior que zero para motorista");
            }
        }

        return new WeeklyRoute
        {
            Id = id,
            UserId = GetRequiredStringField(fields, "userId"),
            Role = role,
            Origin = GetRequiredStringField(fields, "origin"),
            Destination = GetRequiredStringField(fields, "destination"),
            DaysOfWeek = daysOfWeek,
            DepartureTimeMinutes = departureTimeMinutes,
            AvailableSeats = availableSeats,
            CreatedAtUtc = GetRequiredTimestampField(fields, "createdAtUtc")
        };
    }

    private string GetAuthenticatedUserId()
    {
        var user = _authService.CurrentUser
            ?? throw new InvalidOperationException(
                "Não há uma sessão autenticada. Entre novamente para continuar.");

        if (string.IsNullOrWhiteSpace(user.Id))
        {
            throw new InvalidOperationException(
                "A sessão autenticada não contém um identificador de usuário válido.");
        }

        return user.Id;
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
        string fieldName)
    {
        if (!fields.TryGetValue(fieldName, out var field)
            || field.ArrayValue is null
            || field.ArrayValue.Values.Count == 0)
        {
            throw new InvalidOperationException(
                $"O documento de rota não contém o campo obrigatório '{fieldName}'.");
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
                throw new InvalidOperationException(
                    $"O documento de rota contém um valor inválido em '{fieldName}'.");
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
        string fieldName)
    {
        if (fields.TryGetValue(fieldName, out var field)
            && !string.IsNullOrWhiteSpace(field.StringValue))
        {
            return field.StringValue;
        }

        throw new InvalidOperationException(
            $"O documento de rota não contém o campo obrigatório '{fieldName}'.");
    }

    private static int GetRequiredInt32Field(
        IReadOnlyDictionary<string, FirestoreValueDto> fields,
        string fieldName)
    {
        if (!fields.TryGetValue(fieldName, out var field))
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
                "A coleção de rotas semanais não foi encontrada no Firebase.",
            _ => "Não foi possível concluir a operação de rotas no Firebase."
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
        return $"{FirestoreBaseUrl}/projects/{Escape(_options.ProjectId)}" +
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
    }

    private sealed class FirestoreValueDto
    {
        [JsonPropertyName("stringValue")]
        public string? StringValue { get; init; }

        [JsonPropertyName("integerValue")]
        public JsonElement IntegerValue { get; init; }

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
