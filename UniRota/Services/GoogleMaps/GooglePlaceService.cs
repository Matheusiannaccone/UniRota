using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using UniRota.Models;
using UniRota.Services.Firebase;
using UniRota.Services.Interfaces;

namespace UniRota.Services.GoogleMaps;

public sealed class GooglePlaceService : IPlaceService
{
    private const string AutocompleteFunctionName = "placesAutocomplete";
    private const string DetailsFunctionName = "placeDetails";
    private const string CoordinatesFunctionName = "placeCoordinates";
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(10);
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;
    private readonly FirebaseOptions _options;
    private readonly IAuthService _authService;

    public GooglePlaceService(
        HttpClient httpClient,
        FirebaseOptions options,
        IAuthService authService)
    {
        _httpClient = httpClient;
        _options = options;
        _authService = authService;
    }

    public PlaceAutocompleteSession CreateSession()
    {
        return new PlaceAutocompleteSession(Guid.NewGuid().ToString());
    }

    public async Task<IReadOnlyList<PlaceSuggestion>> SearchAsync(
        string query,
        PlaceAutocompleteSession session,
        CancellationToken cancellationToken = default)
    {
        var normalizedQuery = query?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(normalizedQuery))
        {
            return [];
        }

        ValidateSession(session);

        var response = await CallAsync<AutocompleteResultDto>(
            AutocompleteFunctionName,
            new
            {
                input = normalizedQuery,
                sessionToken = session.Id
            },
            cancellationToken);

        return response.Suggestions
            .Where(suggestion =>
                !string.IsNullOrWhiteSpace(suggestion.PlaceId)
                && !string.IsNullOrWhiteSpace(suggestion.DisplayText))
            .Select(suggestion => new PlaceSuggestion
            {
                PlaceId = suggestion.PlaceId!.Trim(),
                DisplayText = suggestion.DisplayText!.Trim()
            })
            .ToArray();
    }

    public async Task<SelectedPlace> GetPlaceAsync(
        string placeId,
        PlaceAutocompleteSession session,
        CancellationToken cancellationToken = default)
    {
        var normalizedPlaceId = placeId?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(normalizedPlaceId))
        {
            throw new ArgumentException(
                "Informe uma sugestão de endereço válida.",
                nameof(placeId));
        }

        ValidateSession(session);

        var response = await CallAsync<PlaceDetailsResultDto>(
            DetailsFunctionName,
            new
            {
                placeId = normalizedPlaceId,
                sessionToken = session.Id
            },
            cancellationToken);

        if (string.IsNullOrWhiteSpace(response.PlaceId)
            || string.IsNullOrWhiteSpace(response.Address))
        {
            throw new InvalidOperationException(
                "O serviço de endereços retornou uma resposta inválida.");
        }

        return new SelectedPlace
        {
            PlaceId = response.PlaceId.Trim(),
            Address = response.Address.Trim()
        };
    }

    public async Task<IReadOnlyDictionary<string, MapCoordinate>>
        GetCoordinatesAsync(
            IReadOnlyList<string> placeIds,
            CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(placeIds);

        var normalizedPlaceIds = placeIds
            .Select((placeId, index) => RequirePlaceId(
                placeId,
                $"{nameof(placeIds)}[{index}]"))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (normalizedPlaceIds.Length == 0)
        {
            return new Dictionary<string, MapCoordinate>(StringComparer.Ordinal);
        }

        if (normalizedPlaceIds.Length > 4)
        {
            throw new ArgumentException(
                "É possível consultar no máximo quatro pontos do trajeto.",
                nameof(placeIds));
        }

        var response = await CallAsync<PlaceCoordinatesResultDto>(
            CoordinatesFunctionName,
            new { placeIds = normalizedPlaceIds },
            cancellationToken);
        var coordinates = new Dictionary<string, MapCoordinate>(
            StringComparer.Ordinal);

        foreach (var item in response.Coordinates)
        {
            var placeId = item.PlaceId?.Trim() ?? string.Empty;
            var coordinate = new MapCoordinate(item.Latitude, item.Longitude);

            if (!normalizedPlaceIds.Contains(placeId, StringComparer.Ordinal)
                || !coordinate.IsValid)
            {
                throw new InvalidOperationException(
                    "O serviço de endereços retornou coordenadas inválidas.");
            }

            coordinates.TryAdd(placeId, coordinate);
        }

        return coordinates;
    }

    private async Task<TResult> CallAsync<TResult>(
        string functionName,
        object data,
        CancellationToken cancellationToken)
    {
        EnsureConfigured();

        var idToken = await _authService.GetValidIdTokenAsync(cancellationToken);
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            BuildFunctionUrl(functionName))
        {
            Content = new StringContent(
                JsonSerializer.Serialize(new { data }, JsonOptions),
                Encoding.UTF8,
                "application/json")
        };
        request.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", idToken);

        using var timeoutSource =
            CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(RequestTimeout);

        try
        {
            using var response = await _httpClient.SendAsync(
                request,
                timeoutSource.Token);
            var content = await response.Content.ReadAsStringAsync(
                timeoutSource.Token);

            if (!response.IsSuccessStatusCode)
            {
                throw CreateServiceException(response.StatusCode, content);
            }

            var envelope = Deserialize<CallableResponseDto<TResult>>(content);

            return envelope.Result
                ?? throw new InvalidOperationException(
                    "O serviço de endereços retornou uma resposta vazia.");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new InvalidOperationException(
                "A busca de endereços demorou mais que o esperado. Tente novamente.");
        }
        catch (HttpRequestException exception)
        {
            throw new InvalidOperationException(
                "Não foi possível consultar endereços. Verifique sua conexão.",
                exception);
        }
    }

    private static void ValidateSession(PlaceAutocompleteSession? session)
    {
        if (session is null || !Guid.TryParseExact(session.Id, "D", out _))
        {
            throw new ArgumentException(
                "A sessão de busca de endereços é inválida.",
                nameof(session));
        }
    }

    private static string RequirePlaceId(string? placeId, string parameterName)
    {
        var normalized = placeId?.Trim() ?? string.Empty;

        if (normalized.Length is < 1 or > 300)
        {
            throw new ArgumentException(
                "Informe um Place ID válido.",
                parameterName);
        }

        return normalized;
    }

    private void EnsureConfigured()
    {
        if (string.IsNullOrWhiteSpace(_options.ProjectId)
            || string.IsNullOrWhiteSpace(_options.FunctionsRegion))
        {
            throw new InvalidOperationException(
                "Configure o projeto e a região das Firebase Functions.");
        }
    }

    private string BuildFunctionUrl(string functionName)
    {
        return $"https://{Uri.EscapeDataString(_options.FunctionsRegion)}-" +
               $"{Uri.EscapeDataString(_options.ProjectId)}.cloudfunctions.net/" +
               Uri.EscapeDataString(functionName);
    }

    private static InvalidOperationException CreateServiceException(
        HttpStatusCode statusCode,
        string content)
    {
        var errorStatus = TryGetErrorStatus(content);
        var message = errorStatus switch
        {
            "UNAUTHENTICATED" =>
                "Sua sessão expirou. Entre novamente para buscar endereços.",
            "RESOURCE_EXHAUSTED" =>
                "O limite de buscas de endereço foi atingido. Tente novamente mais tarde.",
            "INVALID_ARGUMENT" =>
                "Não foi possível pesquisar esse endereço.",
            "DEADLINE_EXCEEDED" =>
                "A busca de endereços demorou mais que o esperado. Tente novamente.",
            _ when statusCode == HttpStatusCode.TooManyRequests =>
                "O limite de buscas de endereço foi atingido. Tente novamente mais tarde.",
            _ => "Não foi possível consultar endereços agora. Tente novamente."
        };

        var exception = new InvalidOperationException(message);
        exception.Data["FunctionStatus"] = errorStatus;
        exception.Data["FunctionResponse"] = content;
        return exception;
    }

    private static string TryGetErrorStatus(string content)
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
        }
        catch (JsonException)
        {
        }

        return "UNKNOWN_ERROR";
    }

    private static T Deserialize<T>(string content)
    {
        try
        {
            return JsonSerializer.Deserialize<T>(content, JsonOptions)
                ?? throw new JsonException("A resposta estava vazia.");
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException(
                "O serviço de endereços retornou uma resposta inválida.",
                exception);
        }
    }

    private sealed class CallableResponseDto<TResult>
    {
        [JsonPropertyName("result")]
        public TResult? Result { get; init; }
    }

    private sealed class AutocompleteResultDto
    {
        [JsonPropertyName("suggestions")]
        public List<PlaceSuggestionDto> Suggestions { get; init; } = [];
    }

    private sealed class PlaceSuggestionDto
    {
        [JsonPropertyName("placeId")]
        public string? PlaceId { get; init; }

        [JsonPropertyName("displayText")]
        public string? DisplayText { get; init; }
    }

    private sealed class PlaceDetailsResultDto
    {
        [JsonPropertyName("placeId")]
        public string PlaceId { get; init; } = string.Empty;

        [JsonPropertyName("address")]
        public string Address { get; init; } = string.Empty;
    }

    private sealed class PlaceCoordinatesResultDto
    {
        [JsonPropertyName("coordinates")]
        public List<PlaceCoordinateDto> Coordinates { get; init; } = [];
    }

    private sealed class PlaceCoordinateDto
    {
        [JsonPropertyName("placeId")]
        public string? PlaceId { get; init; }

        [JsonPropertyName("latitude")]
        public double Latitude { get; init; }

        [JsonPropertyName("longitude")]
        public double Longitude { get; init; }
    }
}
