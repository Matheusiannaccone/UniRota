using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using UniRota.Models;
using UniRota.Services.Firebase;
using UniRota.Services.Interfaces;

namespace UniRota.Services.GoogleMaps;

public sealed class GoogleMapRouteService : IMapRouteService
{
    private const string ComputeRouteFunctionName = "computeRoute";
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(20);
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;
    private readonly FirebaseOptions _options;
    private readonly IAuthService _authService;

    public GoogleMapRouteService(
        HttpClient httpClient,
        FirebaseOptions options,
        IAuthService authService)
    {
        _httpClient = httpClient;
        _options = options;
        _authService = authService;
    }

    public async Task<MapRouteResult> CalculateAsync(
        string originPlaceId,
        string destinationPlaceId,
        IReadOnlyList<string>? intermediatePlaceIds = null,
        CancellationToken cancellationToken = default)
    {
        var normalizedOriginPlaceId = RequirePlaceId(
            originPlaceId,
            nameof(originPlaceId));
        var normalizedDestinationPlaceId = RequirePlaceId(
            destinationPlaceId,
            nameof(destinationPlaceId));
        var normalizedIntermediatePlaceIds = NormalizeIntermediatePlaceIds(
            normalizedOriginPlaceId,
            normalizedDestinationPlaceId,
            intermediatePlaceIds);

        EnsureConfigured();

        var idToken = await GetIdTokenAsync(cancellationToken);
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            BuildFunctionUrl())
        {
            Content = new StringContent(
                JsonSerializer.Serialize(
                    new
                    {
                        data = new
                        {
                            originPlaceId = normalizedOriginPlaceId,
                            destinationPlaceId = normalizedDestinationPlaceId,
                            intermediatePlaceIds = normalizedIntermediatePlaceIds
                        }
                    },
                    JsonOptions),
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

            var envelope = Deserialize<CallableResponseDto<RouteResultDto>>(content);
            var result = envelope.Result
                ?? throw new InvalidOperationException(
                    "O serviço de rotas retornou uma resposta vazia.");

            if (result.DistanceMeters <= 0
                || !double.IsFinite(result.DurationSeconds)
                || result.DurationSeconds <= 0
                || result.DurationSeconds > TimeSpan.MaxValue.TotalSeconds)
            {
                throw new InvalidOperationException(
                    "O serviço de rotas retornou uma resposta inválida.");
            }

            return new MapRouteResult
            {
                DistanceMeters = result.DistanceMeters,
                Duration = TimeSpan.FromSeconds(result.DurationSeconds)
            };
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new InvalidOperationException(
                "O cálculo da rota demorou mais que o esperado. Tente novamente.");
        }
        catch (HttpRequestException exception)
        {
            throw new InvalidOperationException(
                "Não foi possível calcular a rota. Verifique sua conexão e tente novamente.",
                exception);
        }
    }

    private static string RequirePlaceId(string value, string parameterName)
    {
        var normalized = value?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(normalized) || normalized.Length > 300)
        {
            throw new ArgumentException(
                "Selecione um endereço válido nas sugestões.",
                parameterName);
        }

        return normalized;
    }

    private static IReadOnlyList<string> NormalizeIntermediatePlaceIds(
        string originPlaceId,
        string destinationPlaceId,
        IReadOnlyList<string>? intermediatePlaceIds)
    {
        if (intermediatePlaceIds is null || intermediatePlaceIds.Count == 0)
        {
            return [];
        }

        if (intermediatePlaceIds.Count > 2)
        {
            throw new ArgumentException(
                "A rota aceita no máximo dois pontos intermediários.",
                nameof(intermediatePlaceIds));
        }

        var normalized = intermediatePlaceIds
            .Select((placeId, index) => RequirePlaceId(
                placeId,
                $"{nameof(intermediatePlaceIds)}[{index}]"))
            .ToArray();
        var sequence = new[] { originPlaceId }
            .Concat(normalized)
            .Append(destinationPlaceId)
            .ToArray();

        for (var index = 1; index < sequence.Length; index++)
        {
            if (string.Equals(
                    sequence[index - 1],
                    sequence[index],
                    StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "A rota não pode possuir pontos consecutivos duplicados.",
                    nameof(intermediatePlaceIds));
            }
        }

        return normalized;
    }

    private async Task<string> GetIdTokenAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            return await _authService.GetValidIdTokenAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            var authenticationException = new InvalidOperationException(
                "Não foi possível validar sua sessão. Entre novamente e tente de novo.",
                exception);
            authenticationException.Data["FunctionStatus"] = "UNAUTHENTICATED";
            throw authenticationException;
        }
    }

    private void EnsureConfigured()
    {
        if (string.IsNullOrWhiteSpace(_options.ProjectId)
            || string.IsNullOrWhiteSpace(_options.FunctionsRegion))
        {
            var exception = new InvalidOperationException(
                "Configure o projeto e a região das Firebase Functions.");
            exception.Data["FunctionStatus"] = "CONFIGURATION_ERROR";
            throw exception;
        }
    }

    private string BuildFunctionUrl()
    {
        return $"https://{Uri.EscapeDataString(_options.FunctionsRegion)}-" +
               $"{Uri.EscapeDataString(_options.ProjectId)}.cloudfunctions.net/" +
               ComputeRouteFunctionName;
    }

    private static InvalidOperationException CreateServiceException(
        HttpStatusCode statusCode,
        string content)
    {
        var errorStatus = TryGetErrorStatus(content);
        var message = errorStatus switch
        {
            "UNAUTHENTICATED" =>
                "Sua sessão expirou. Entre novamente para calcular a rota.",
            "RESOURCE_EXHAUSTED" =>
                "O limite de cálculos de rota foi atingido. Tente novamente mais tarde.",
            "INVALID_ARGUMENT" =>
                "Não foi possível calcular a rota com os endereços selecionados.",
            "FAILED_PRECONDITION" =>
                "Não foi encontrada uma rota de carro entre os endereços selecionados.",
            "DEADLINE_EXCEEDED" =>
                "O cálculo da rota demorou mais que o esperado. Tente novamente.",
            "UNAVAILABLE" =>
                "O serviço de rotas está indisponível. Tente novamente.",
            _ when statusCode == HttpStatusCode.TooManyRequests =>
                "O limite de cálculos de rota foi atingido. Tente novamente mais tarde.",
            _ => "Não foi possível calcular a rota. Tente novamente."
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
                "O serviço de rotas retornou uma resposta inválida.",
                exception);
        }
    }

    private sealed class CallableResponseDto<TResult>
    {
        [JsonPropertyName("result")]
        public TResult? Result { get; init; }
    }

    private sealed class RouteResultDto
    {
        [JsonPropertyName("distanceMeters")]
        public long DistanceMeters { get; init; }

        [JsonPropertyName("durationSeconds")]
        public double DurationSeconds { get; init; }
    }
}
