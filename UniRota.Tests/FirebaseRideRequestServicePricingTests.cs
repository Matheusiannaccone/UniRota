using System.Net;
using System.Text;
using System.Text.Json;
using UniRota.Models;
using UniRota.Services.Firebase;
using UniRota.Services.Interfaces;

namespace UniRota.Tests;

public sealed class FirebaseRideRequestServicePricingTests
{
    [Fact]
    public async Task CreateAsync_PersistsAndReturnsSuggestedPrice()
    {
        string? commitBody = null;
        var handler = new StubHttpMessageHandler(async (request, cancellationToken) =>
        {
            var url = request.RequestUri?.AbsoluteUri ?? string.Empty;

            if (request.Method == HttpMethod.Get
                && url.Contains("/weeklyRoutes/driver-route", StringComparison.Ordinal))
            {
                return JsonResponse(CreateDriverRouteDocumentJson());
            }

            if (url.EndsWith(":runQuery", StringComparison.Ordinal))
            {
                return JsonResponse("[]");
            }

            if (url.EndsWith(":commit", StringComparison.Ordinal))
            {
                commitBody = await request.Content!.ReadAsStringAsync(
                    cancellationToken);
                return JsonResponse("{}");
            }

            throw new InvalidOperationException($"Requisição inesperada: {url}");
        });
        var passengerRoute = CreatePassengerRoute();
        var service = CreateService(handler, [passengerRoute]);

        var result = await service.CreateAsync(
            passengerRoute.Id,
            CreateMatch(),
            RideRequestType.Weekly,
            null,
            3.45m);

        Assert.Equal(3.45m, result.SuggestedPrice);
        Assert.NotNull(commitBody);

        using var document = JsonDocument.Parse(commitBody);
        var writes = document.RootElement.GetProperty("writes");
        Assert.Equal(2, writes.GetArrayLength());
        Assert.Equal(
            3.45m,
            writes[0]
                .GetProperty("update")
                .GetProperty("fields")
                .GetProperty("suggestedPrice")
                .GetProperty("doubleValue")
                .GetDecimal());
        Assert.True(
            writes[1]
                .GetProperty("update")
                .GetProperty("fields")
                .TryGetProperty("requestRevision", out _));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task CreateAsync_RejectsNonPositiveSuggestedPrice(
        int suggestedPrice)
    {
        var handler = new StubHttpMessageHandler((request, cancellationToken) =>
            throw new InvalidOperationException("Não deveria acessar a rede."));
        var service = CreateService(handler, [CreatePassengerRoute()]);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            service.CreateAsync(
                "passenger-route",
                CreateMatch(),
                RideRequestType.Weekly,
                null,
                suggestedPrice));

        Assert.Equal(0, handler.RequestCount);
    }

    [Fact]
    public async Task GetMyPendingRequestsAsync_DeserializesSuggestedPrice()
    {
        var handler = new StubHttpMessageHandler((request, cancellationToken) =>
            Task.FromResult(JsonResponse(CreateRideRequestQueryJson())));
        var service = CreateService(handler, []);

        var request = Assert.Single(
            await service.GetMyPendingRequestsAsync());

        Assert.Equal(3.45m, request.SuggestedPrice);
    }

    private static FirebaseRideRequestService CreateService(
        HttpMessageHandler handler,
        IReadOnlyList<WeeklyRoute> routes)
    {
        return new FirebaseRideRequestService(
            new HttpClient(handler),
            new FirebaseOptions
            {
                ApiKey = "test-api-key",
                ProjectId = "test-project"
            },
            new FakeAuthService(),
            new FakeRouteService(routes));
    }

    private static WeeklyRoute CreatePassengerRoute()
    {
        return new WeeklyRoute
        {
            Id = "passenger-route",
            UserId = "passenger-user",
            Role = RouteRole.Passenger,
            Origin = "Centro",
            Destination = "Facens",
            DaysOfWeek = [DayOfWeek.Monday],
            DepartureTimeMinutes = 480
        };
    }

    private static MatchResult CreateMatch()
    {
        var driverRoute = new WeeklyRoute
        {
            Id = "driver-route",
            UserId = "driver-user",
            UserName = "Motorista",
            Role = RouteRole.Driver,
            Origin = "Centro",
            Destination = "Facens",
            DaysOfWeek = [DayOfWeek.Monday],
            DepartureTimeMinutes = 480,
            AvailableSeats = 2,
            EstimatedDistanceKm = 12.75m
        };

        return new MatchResult(driverRoute, [DayOfWeek.Monday], 0);
    }

    private static string CreateDriverRouteDocumentJson()
    {
        return """
            {
              "name": "projects/test-project/databases/(default)/documents/weeklyRoutes/driver-route",
              "fields": {
                "userId": { "stringValue": "driver-user" },
                "role": { "stringValue": "driver" },
                "availableSeats": { "integerValue": "2" },
                "requestRevision": { "integerValue": "0" }
              },
              "updateTime": "2026-09-03T12:00:00Z"
            }
            """;
    }

    private static string CreateRideRequestQueryJson()
    {
        return """
            [
              {
                "document": {
                  "name": "projects/test-project/databases/(default)/documents/rideRequests/request-1",
                  "fields": {
                    "passengerUserId": { "stringValue": "passenger-user" },
                    "passengerUserName": { "stringValue": "Passageiro" },
                    "driverUserId": { "stringValue": "driver-user" },
                    "driverUserName": { "stringValue": "Motorista" },
                    "passengerRouteId": { "stringValue": "passenger-route" },
                    "driverRouteId": { "stringValue": "driver-route" },
                    "compatibleDays": {
                      "arrayValue": {
                        "values": [ { "stringValue": "Monday" } ]
                      }
                    },
                    "type": { "stringValue": "weekly" },
                    "status": { "stringValue": "pending" },
                    "requestedDate": { "nullValue": null },
                    "suggestedPrice": { "doubleValue": 3.45 },
                    "createdAtUtc": { "timestampValue": "2026-09-03T12:00:00Z" }
                  }
                }
              }
            ]
            """;
    }

    private static HttpResponseMessage JsonResponse(string content)
    {
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(content, Encoding.UTF8, "application/json")
        };
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken,
            Task<HttpResponseMessage>> _sendAsync;

        public StubHttpMessageHandler(
            Func<HttpRequestMessage, CancellationToken,
                Task<HttpResponseMessage>> sendAsync)
        {
            _sendAsync = sendAsync;
        }

        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestCount++;
            return _sendAsync(request, cancellationToken);
        }
    }

    private sealed class FakeAuthService : IAuthService
    {
        public User? CurrentUser { get; } = new()
        {
            Id = "passenger-user",
            Name = "Passageiro",
            Email = "passageiro@facens.br"
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

    private sealed class FakeRouteService : IRouteService
    {
        private readonly IReadOnlyList<WeeklyRoute> _routes;

        public FakeRouteService(IReadOnlyList<WeeklyRoute> routes)
        {
            _routes = routes;
        }

        public Task<WeeklyRoute> CreateAsync(
            WeeklyRoute route,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<WeeklyRoute> UpdateAsync(
            WeeklyRoute route,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task DeleteAsync(
            string routeId,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<WeeklyRoute>> GetMyRoutesAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult(_routes);

        public Task<IReadOnlyList<WeeklyRoute>> GetDriverRoutesAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<WeeklyRoute>>([]);
    }
}
