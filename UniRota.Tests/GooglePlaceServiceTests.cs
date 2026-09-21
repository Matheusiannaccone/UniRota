using System.Net;
using System.Text.Json;
using UniRota.Models;
using UniRota.Services.Firebase;
using UniRota.Services.GoogleMaps;
using UniRota.Services.Interfaces;

namespace UniRota.Tests;

public sealed class GooglePlaceServiceTests
{
    [Fact]
    public void CreateSession_ReturnsUniqueVersion4Uuids()
    {
        var service = CreateService(new StubHttpMessageHandler(
            (request, cancellationToken) =>
                throw new InvalidOperationException("Não deveria acessar a rede.")));

        var first = service.CreateSession();
        var second = service.CreateSession();

        Assert.NotEqual(first.Id, second.Id);
        Assert.True(Guid.TryParseExact(first.Id, "D", out _));
        Assert.Equal('4', first.Id[14]);
        Assert.True(Guid.TryParseExact(second.Id, "D", out _));
        Assert.Equal('4', second.Id[14]);
    }

    [Fact]
    public async Task SearchAsync_CallsAuthenticatedProxyAndParsesSuggestions()
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
                    "suggestions": [
                      {
                        "placeId": "place-1",
                        "displayText": "Facens, Sorocaba"
                      }
                    ]
                  }
                }
                """);
        });
        var service = CreateService(handler);
        var session = service.CreateSession();

        var suggestions = await service.SearchAsync("  Facens  ", session);

        var suggestion = Assert.Single(suggestions);
        Assert.Equal("place-1", suggestion.PlaceId);
        Assert.Equal("Facens, Sorocaba", suggestion.DisplayText);
        Assert.Equal("Bearer", capturedRequest!.Headers.Authorization!.Scheme);
        Assert.Equal("test-token", capturedRequest.Headers.Authorization.Parameter);
        Assert.EndsWith("/placesAutocomplete", capturedRequest.RequestUri!.AbsoluteUri);

        using var document = JsonDocument.Parse(capturedBody!);
        var data = document.RootElement.GetProperty("data");
        Assert.Equal("Facens", data.GetProperty("input").GetString());
        Assert.Equal(session.Id, data.GetProperty("sessionToken").GetString());
    }

    [Fact]
    public async Task GetPlaceAsync_UsesSameSessionAndParsesSelectedAddress()
    {
        string? capturedBody = null;
        var handler = new StubHttpMessageHandler(async (request, cancellationToken) =>
        {
            capturedBody = await request.Content!.ReadAsStringAsync(cancellationToken);
            return JsonResponse("""
                {
                  "result": {
                    "placeId": "canonical-place-id",
                    "address": "Rodovia Senador José Ermírio de Moraes, Sorocaba"
                  }
                }
                """);
        });
        var service = CreateService(handler);
        var session = service.CreateSession();

        var place = await service.GetPlaceAsync("place-1", session);

        Assert.Equal("canonical-place-id", place.PlaceId);
        Assert.Equal(
            "Rodovia Senador José Ermírio de Moraes, Sorocaba",
            place.Address);

        using var document = JsonDocument.Parse(capturedBody!);
        var data = document.RootElement.GetProperty("data");
        Assert.Equal("place-1", data.GetProperty("placeId").GetString());
        Assert.Equal(session.Id, data.GetProperty("sessionToken").GetString());
    }

    [Fact]
    public async Task SearchAsync_EmptyQueryDoesNotCallProxy()
    {
        var handler = new StubHttpMessageHandler(
            (request, cancellationToken) =>
                throw new InvalidOperationException("Não deveria acessar a rede."));
        var service = CreateService(handler);

        var suggestions = await service.SearchAsync(
            "   ",
            service.CreateSession());

        Assert.Empty(suggestions);
        Assert.Equal(0, handler.RequestCount);
    }

    [Fact]
    public async Task GetCoordinatesAsync_DeduplicatesPlaceIdsAndParsesLocations()
    {
        string? capturedBody = null;
        var handler = new StubHttpMessageHandler(async (request, cancellationToken) =>
        {
            capturedBody = await request.Content!.ReadAsStringAsync(
                cancellationToken);
            return JsonResponse("""
                {
                  "result": {
                    "coordinates": [
                      {
                        "placeId": "place-1",
                        "latitude": -23.4708,
                        "longitude": -47.4287
                      },
                      {
                        "placeId": "place-2",
                        "latitude": -23.5015,
                        "longitude": -47.4526
                      }
                    ]
                  }
                }
                """);
        });
        var service = CreateService(handler);

        var coordinates = await service.GetCoordinatesAsync(
            [" place-1 ", "place-1", "place-2"]);

        Assert.Equal(2, coordinates.Count);
        Assert.Equal(new MapCoordinate(-23.4708, -47.4287), coordinates["place-1"]);
        Assert.Equal(new MapCoordinate(-23.5015, -47.4526), coordinates["place-2"]);

        using var document = JsonDocument.Parse(capturedBody!);
        Assert.Equal(
            ["place-1", "place-2"],
            document.RootElement
                .GetProperty("data")
                .GetProperty("placeIds")
                .EnumerateArray()
                .Select(item => item.GetString()));
    }

    [Fact]
    public async Task SearchAsync_QuotaErrorReturnsUsefulMessage()
    {
        var handler = new StubHttpMessageHandler((request, cancellationToken) =>
            Task.FromResult(JsonResponse(
                """
                {
                  "error": {
                    "status": "RESOURCE_EXHAUSTED",
                    "message": "Quota exceeded"
                  }
                }
                """,
                HttpStatusCode.TooManyRequests)));
        var service = CreateService(handler);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.SearchAsync("Facens", service.CreateSession()));

        Assert.Contains("limite", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("RESOURCE_EXHAUSTED", exception.Data["FunctionStatus"]);
    }

    [Fact]
    public async Task SearchAsync_InvalidResponseFailsSafely()
    {
        var handler = new StubHttpMessageHandler((request, cancellationToken) =>
            Task.FromResult(JsonResponse("not-json")));
        var service = CreateService(handler);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.SearchAsync("Facens", service.CreateSession()));

        Assert.Contains("resposta inválida", exception.Message);
    }

    [Fact]
    public async Task SearchAsync_NetworkFailureReturnsUsefulMessage()
    {
        var handler = new StubHttpMessageHandler((request, cancellationToken) =>
            throw new HttpRequestException("offline"));
        var service = CreateService(handler);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.SearchAsync("Facens", service.CreateSession()));

        Assert.Contains("conexão", exception.Message);
    }

    private static GooglePlaceService CreateService(HttpMessageHandler handler)
    {
        return new GooglePlaceService(
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
