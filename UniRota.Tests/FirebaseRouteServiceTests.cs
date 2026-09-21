using System.Net;
using System.Text.Json;
using UniRota.Models;
using UniRota.Services.Firebase;
using UniRota.Services.Interfaces;

namespace UniRota.Tests;

public sealed class FirebaseRouteServiceTests
{
    [Fact]
    public async Task CreateAsync_PersistsAndDeserializesPlaceIds()
    {
        string? requestBody = null;
        var handler = new StubHttpMessageHandler(async (request, cancellationToken) =>
        {
            requestBody = await request.Content!.ReadAsStringAsync(cancellationToken);
            return JsonResponse(CreateRouteDocumentJson(
                "created-route",
                "origin-place-id",
                "destination-place-id"));
        });
        var service = CreateService(handler);

        var route = await service.CreateAsync(CreateDriverRoute(
            id: string.Empty,
            originPlaceId: "  origin-place-id  ",
            destinationPlaceId: "  destination-place-id  "));

        Assert.Equal("origin-place-id", route.OriginPlaceId);
        Assert.Equal("destination-place-id", route.DestinationPlaceId);
        Assert.NotNull(requestBody);

        using var document = JsonDocument.Parse(requestBody);
        var fields = document.RootElement.GetProperty("fields");
        Assert.Equal(
            "origin-place-id",
            fields.GetProperty("originPlaceId").GetProperty("stringValue").GetString());
        Assert.Equal(
            "destination-place-id",
            fields.GetProperty("destinationPlaceId").GetProperty("stringValue").GetString());
    }

    [Fact]
    public async Task UpdateAsync_PersistsAndDeserializesPlaceIds()
    {
        string? updateBody = null;
        string? updateUrl = null;
        var handler = new StubHttpMessageHandler(async (request, cancellationToken) =>
        {
            if (request.Method == HttpMethod.Get)
            {
                return JsonResponse(CreateRouteDocumentJson("route-1"));
            }

            updateUrl = request.RequestUri?.AbsoluteUri;
            updateBody = await request.Content!.ReadAsStringAsync(cancellationToken);
            return JsonResponse(CreateRouteDocumentJson(
                "route-1",
                "new-origin-place-id",
                "new-destination-place-id"));
        });
        var service = CreateService(handler);

        var route = await service.UpdateAsync(CreateDriverRoute(
            id: "route-1",
            originPlaceId: "new-origin-place-id",
            destinationPlaceId: "new-destination-place-id"));

        Assert.Equal("new-origin-place-id", route.OriginPlaceId);
        Assert.Equal("new-destination-place-id", route.DestinationPlaceId);
        Assert.Contains("updateMask.fieldPaths=originPlaceId", updateUrl);
        Assert.Contains("updateMask.fieldPaths=destinationPlaceId", updateUrl);
        Assert.NotNull(updateBody);

        using var document = JsonDocument.Parse(updateBody);
        var fields = document.RootElement.GetProperty("fields");
        Assert.Equal(
            "new-origin-place-id",
            fields.GetProperty("originPlaceId").GetProperty("stringValue").GetString());
        Assert.Equal(
            "new-destination-place-id",
            fields.GetProperty("destinationPlaceId").GetProperty("stringValue").GetString());
    }

    [Fact]
    public async Task GetMyRoutesAsync_DeserializesLegacyDocumentWithoutPlaceIds()
    {
        var handler = new StubHttpMessageHandler((request, cancellationToken) =>
            Task.FromResult(JsonResponse(
                $"[{{\"document\":{CreateRouteDocumentJson("legacy-route")}}}]")));
        var service = CreateService(handler);

        var route = Assert.Single(await service.GetMyRoutesAsync());

        Assert.Equal("legacy-route", route.Id);
        Assert.Equal(string.Empty, route.OriginPlaceId);
        Assert.Equal(string.Empty, route.DestinationPlaceId);
    }

    private static FirebaseRouteService CreateService(HttpMessageHandler handler)
    {
        return new FirebaseRouteService(
            new HttpClient(handler),
            new FirebaseOptions
            {
                ApiKey = "test-api-key",
                ProjectId = "test-project"
            },
            new FakeAuthService());
    }

    private static WeeklyRoute CreateDriverRoute(
        string id,
        string originPlaceId,
        string destinationPlaceId)
    {
        return new WeeklyRoute
        {
            Id = id,
            Role = RouteRole.Driver,
            Origin = "Centro",
            OriginPlaceId = originPlaceId,
            Destination = "Facens",
            DestinationPlaceId = destinationPlaceId,
            DaysOfWeek = [DayOfWeek.Monday],
            DepartureTimeMinutes = 450,
            AvailableSeats = 2,
            EstimatedDistanceKm = 8.5m
        };
    }

    private static string CreateRouteDocumentJson(
        string routeId,
        string? originPlaceId = null,
        string? destinationPlaceId = null)
    {
        var originPlaceIdField = originPlaceId is null
            ? string.Empty
            : $",\"originPlaceId\":{{\"stringValue\":\"{originPlaceId}\"}}";
        var destinationPlaceIdField = destinationPlaceId is null
            ? string.Empty
            : $",\"destinationPlaceId\":{{\"stringValue\":\"{destinationPlaceId}\"}}";

        return $$"""
            {
              "name": "projects/test-project/databases/(default)/documents/weeklyRoutes/{{routeId}}",
              "fields": {
                "userId": { "stringValue": "user-1" },
                "userName": { "stringValue": "Usuário" },
                "role": { "stringValue": "driver" },
                "origin": { "stringValue": "Centro" }{{originPlaceIdField}},
                "destination": { "stringValue": "Facens" }{{destinationPlaceIdField}},
                "daysOfWeek": {
                  "arrayValue": { "values": [{ "stringValue": "Monday" }] }
                },
                "departureTimeMinutes": { "integerValue": "450" },
                "availableSeats": { "integerValue": "2" },
                "estimatedDistanceKm": { "doubleValue": 8.5 },
                "requestRevision": { "integerValue": "0" },
                "createdAtUtc": { "timestampValue": "2026-09-21T12:00:00Z" }
              },
              "updateTime": "2026-09-21T12:00:00Z"
            }
            """;
    }

    private static HttpResponseMessage JsonResponse(string content)
    {
        return new HttpResponseMessage(HttpStatusCode.OK)
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

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
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
