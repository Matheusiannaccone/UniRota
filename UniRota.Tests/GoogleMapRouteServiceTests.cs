using System.Net;
using System.Text.Json;
using UniRota.Models;
using UniRota.Services.Firebase;
using UniRota.Services.GoogleMaps;
using UniRota.Services.Interfaces;

namespace UniRota.Tests;

public sealed class GoogleMapRouteServiceTests
{
    [Fact]
    public async Task CalculateAsync_CallsAuthenticatedProxyAndParsesRoute()
    {
        HttpRequestMessage? capturedRequest = null;
        string? capturedBody = null;
        var handler = new StubHttpMessageHandler(async (request, cancellationToken) =>
        {
            capturedRequest = request;
            capturedBody = await request.Content!.ReadAsStringAsync(cancellationToken);
            return JsonResponse("""
                {
                  "result": {
                    "distanceMeters": 11840,
                    "durationSeconds": 1325.5,
                    "encodedPolyline": "_p~iF~ps|U_ulLnnqC_mqNvxq`@"
                  }
                }
                """);
        });
        var service = CreateService(handler);

        var result = await service.CalculateAsync(
            "  origin-place-id  ",
            "  destination-place-id  ");

        Assert.Equal(11840, result.DistanceMeters);
        Assert.Equal(TimeSpan.FromSeconds(1325.5), result.Duration);
        Assert.Equal(
            "_p~iF~ps|U_ulLnnqC_mqNvxq`@",
            result.EncodedPolyline);
        Assert.Equal("Bearer", capturedRequest!.Headers.Authorization!.Scheme);
        Assert.Equal("test-token", capturedRequest.Headers.Authorization.Parameter);
        Assert.EndsWith("/computeRoute", capturedRequest.RequestUri!.AbsoluteUri);

        using var document = JsonDocument.Parse(capturedBody!);
        var data = document.RootElement.GetProperty("data");
        Assert.Equal(
            "origin-place-id",
            data.GetProperty("originPlaceId").GetString());
        Assert.Equal(
            "destination-place-id",
            data.GetProperty("destinationPlaceId").GetString());
        Assert.Equal(
            0,
            data.GetProperty("intermediatePlaceIds").GetArrayLength());
    }

    [Fact]
    public async Task CalculateAsync_MissingPolylineReturnsRouteWithoutGeometry()
    {
        var handler = new StubHttpMessageHandler((request, cancellationToken) =>
            Task.FromResult(JsonResponse("""
                {
                  "result": {
                    "distanceMeters": 11840,
                    "durationSeconds": 1325.5
                  }
                }
                """)));
        var service = CreateService(handler);

        var result = await service.CalculateAsync("origin", "destination");

        Assert.Equal(11840, result.DistanceMeters);
        Assert.Equal(TimeSpan.FromSeconds(1325.5), result.Duration);
        Assert.Equal(string.Empty, result.EncodedPolyline);
    }

    [Fact]
    public async Task CalculateAsync_SendsIntermediatePlaceIdsInOrder()
    {
        string? capturedBody = null;
        var handler = new StubHttpMessageHandler(async (request, cancellationToken) =>
        {
            capturedBody = await request.Content!.ReadAsStringAsync(cancellationToken);
            return JsonResponse("""
                {
                  "result": {
                    "distanceMeters": 15000,
                    "durationSeconds": 1800
                  }
                }
                """);
        });
        var service = CreateService(handler);

        await service.CalculateAsync(
            "driver-origin",
            "driver-destination",
            ["passenger-origin", "passenger-destination"]);

        using var document = JsonDocument.Parse(capturedBody!);
        var intermediates = document.RootElement
            .GetProperty("data")
            .GetProperty("intermediatePlaceIds");
        Assert.Equal(
            ["passenger-origin", "passenger-destination"],
            intermediates.EnumerateArray().Select(item => item.GetString()));
    }

    [Fact]
    public async Task CalculateAsync_EmptyRoutesErrorReturnsUsefulMessage()
    {
        var handler = new StubHttpMessageHandler((request, cancellationToken) =>
            Task.FromResult(JsonResponse(
                """
                {
                  "error": {
                    "status": "FAILED_PRECONDITION",
                    "message": "No route was found."
                  }
                }
                """,
                HttpStatusCode.BadRequest)));
        var service = CreateService(handler);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CalculateAsync("origin", "destination"));

        Assert.Contains(
            "não foi encontrada",
            exception.Message,
            StringComparison.OrdinalIgnoreCase);
        Assert.Equal("FAILED_PRECONDITION", exception.Data["FunctionStatus"]);
    }

    [Fact]
    public async Task CalculateAsync_InvalidResponseFailsSafely()
    {
        var handler = new StubHttpMessageHandler((request, cancellationToken) =>
            Task.FromResult(JsonResponse("""
                {
                  "result": {
                    "distanceMeters": 0,
                    "durationSeconds": 0
                  }
                }
                """)));
        var service = CreateService(handler);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CalculateAsync("origin", "destination"));

        Assert.Contains("resposta inválida", exception.Message);
    }

    [Fact]
    public async Task CalculateAsync_NetworkFailureReturnsUsefulMessage()
    {
        var handler = new StubHttpMessageHandler((request, cancellationToken) =>
            throw new HttpRequestException("offline"));
        var service = CreateService(handler);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CalculateAsync("origin", "destination"));

        Assert.Contains("conexão", exception.Message);
    }

    [Fact]
    public async Task CalculateAsync_MissingPlaceIdDoesNotCallProxy()
    {
        var handler = new StubHttpMessageHandler(
            (request, cancellationToken) =>
                throw new InvalidOperationException("Não deveria acessar a rede."));
        var service = CreateService(handler);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CalculateAsync(" ", "destination"));

        Assert.Equal(0, handler.RequestCount);
    }

    [Theory]
    [MemberData(nameof(InvalidIntermediatePlaceIds))]
    public async Task CalculateAsync_InvalidIntermediatesDoNotCallProxy(
        IReadOnlyList<string> intermediatePlaceIds)
    {
        var handler = new StubHttpMessageHandler(
            (request, cancellationToken) =>
                throw new InvalidOperationException("Não deveria acessar a rede."));
        var service = CreateService(handler);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CalculateAsync(
                "origin",
                "destination",
                intermediatePlaceIds));

        Assert.Equal(0, handler.RequestCount);
    }

    public static TheoryData<IReadOnlyList<string>> InvalidIntermediatePlaceIds =>
        new()
        {
            new[] { "one", "two", "three" },
            new[] { " " },
            new[] { "origin" },
            new[] { "one", "one" },
            new[] { "one", "destination" }
        };

    private static GoogleMapRouteService CreateService(HttpMessageHandler handler)
    {
        return new GoogleMapRouteService(
            new HttpClient(handler),
            new FirebaseOptions
            {
                ApiKey = "test-api-key",
                ProjectId = "test-project",
                FunctionsRegion = "southamerica-east1"
            },
            new FakeAuthService());
    }

    private static HttpResponseMessage JsonResponse(
        string content,
        HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        return new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(content)
        };
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>
            _sendAsync;

        public StubHttpMessageHandler(
            Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> sendAsync)
        {
            _sendAsync = sendAsync;
        }

        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            ++RequestCount;
            return _sendAsync(request, cancellationToken);
        }
    }

    private sealed class FakeAuthService : IAuthService
    {
        public User? CurrentUser { get; } = new()
        {
            Id = "user-1",
            Name = "Usuário",
            Email = "usuario@facens.br"
        };

        public Task<User> RegisterAsync(
            string name,
            string email,
            string password,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<User> LoginAsync(
            string email,
            string password,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<User?> RestoreSessionAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult(CurrentUser);

        public Task<string> GetValidIdTokenAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult("test-token");

        public Task LogoutAsync() => Task.CompletedTask;
    }
}
