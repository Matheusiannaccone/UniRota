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
        Assert.Equal(
            "1",
            writes[1]
                .GetProperty("update")
                .GetProperty("fields")
                .GetProperty("requestRevision")
                .GetProperty("integerValue")
                .GetString());
        Assert.Equal(
            "2026-09-03T12:00:00Z",
            writes[1]
                .GetProperty("currentDocument")
                .GetProperty("updateTime")
                .GetString());
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

    [Fact]
    public async Task AcceptAsync_ConsumesLastSeatAndRejectsCompetingRequestsWithoutChangingPrice()
    {
        string? commitBody = null;
        var handler = new StubHttpMessageHandler(async (request, cancellationToken) =>
        {
            var url = request.RequestUri?.AbsoluteUri ?? string.Empty;

            if (request.Method == HttpMethod.Get
                && url.Contains("/rideRequests/request-1", StringComparison.Ordinal))
            {
                return JsonResponse(CreateRideRequestDocumentJson(
                    "request-1",
                    "passenger-user",
                    3.45m));
            }

            if (request.Method == HttpMethod.Get
                && url.Contains("/weeklyRoutes/driver-route", StringComparison.Ordinal))
            {
                return JsonResponse(CreateDriverRouteDocumentJson(1));
            }

            if (url.EndsWith(":runQuery", StringComparison.Ordinal))
            {
                return JsonResponse(CreatePendingRouteRequestsQueryJson());
            }

            if (url.EndsWith(":commit", StringComparison.Ordinal))
            {
                commitBody = await request.Content!.ReadAsStringAsync(
                    cancellationToken);
                return JsonResponse("{}");
            }

            throw new InvalidOperationException($"Requisição inesperada: {url}");
        });
        var service = CreateService(handler, [], "driver-user");

        await service.AcceptAsync("request-1");

        Assert.NotNull(commitBody);
        using var document = JsonDocument.Parse(commitBody);
        var writes = document.RootElement.GetProperty("writes");
        Assert.Equal(3, writes.GetArrayLength());
        Assert.Equal("accepted", GetWrittenStringField(writes[0], "status"));
        Assert.Equal("0", GetWrittenIntegerField(writes[1], "availableSeats"));
        Assert.Equal("rejected", GetWrittenStringField(writes[2], "status"));
        Assert.All(
            writes.EnumerateArray(),
            write => Assert.False(
                write.GetProperty("update")
                    .GetProperty("fields")
                    .TryGetProperty("suggestedPrice", out _)));
        Assert.All(
            writes.EnumerateArray(),
            write => Assert.True(
                write.GetProperty("currentDocument")
                    .TryGetProperty("updateTime", out _)));
    }

    [Fact]
    public async Task RejectAsync_ChangesOnlyStatusAndPreservesSuggestedPriceSnapshot()
    {
        string? commitBody = null;
        var handler = new StubHttpMessageHandler(async (request, cancellationToken) =>
        {
            var url = request.RequestUri?.AbsoluteUri ?? string.Empty;

            if (request.Method == HttpMethod.Get)
            {
                return JsonResponse(CreateRideRequestDocumentJson(
                    "request-1",
                    "passenger-user",
                    3.45m));
            }

            if (url.EndsWith(":commit", StringComparison.Ordinal))
            {
                commitBody = await request.Content!.ReadAsStringAsync(
                    cancellationToken);
                return JsonResponse("{}");
            }

            throw new InvalidOperationException($"Requisição inesperada: {url}");
        });
        var service = CreateService(handler, [], "driver-user");

        await service.RejectAsync("request-1");

        Assert.NotNull(commitBody);
        using var document = JsonDocument.Parse(commitBody);
        var write = Assert.Single(
            document.RootElement.GetProperty("writes").EnumerateArray());
        Assert.Equal("rejected", GetWrittenStringField(write, "status"));
        Assert.False(
            write.GetProperty("update")
                .GetProperty("fields")
                .TryGetProperty("suggestedPrice", out _));
        Assert.Equal(
            "2026-09-03T13:00:00Z",
            write.GetProperty("currentDocument")
                .GetProperty("updateTime")
                .GetString());
    }

    [Fact]
    public async Task AcceptAsync_WithoutSeatsDoesNotCommitAnyChange()
    {
        var handler = new StubHttpMessageHandler((request, cancellationToken) =>
        {
            var url = request.RequestUri?.AbsoluteUri ?? string.Empty;

            if (request.Method == HttpMethod.Get
                && url.Contains("/rideRequests/request-1", StringComparison.Ordinal))
            {
                return Task.FromResult(JsonResponse(
                    CreateRideRequestDocumentJson(
                        "request-1",
                        "passenger-user",
                        3.45m)));
            }

            if (request.Method == HttpMethod.Get
                && url.Contains("/weeklyRoutes/driver-route", StringComparison.Ordinal))
            {
                return Task.FromResult(JsonResponse(
                    CreateDriverRouteDocumentJson(0)));
            }

            throw new InvalidOperationException("Não deveria confirmar a transação.");
        });
        var service = CreateService(handler, [], "driver-user");

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.AcceptAsync("request-1"));

        Assert.Contains("Não há vagas", exception.Message);
        Assert.Equal(2, handler.RequestCount);
    }

    private static FirebaseRideRequestService CreateService(
        HttpMessageHandler handler,
        IReadOnlyList<WeeklyRoute> routes,
        string currentUserId = "passenger-user")
    {
        return new FirebaseRideRequestService(
            new HttpClient(handler),
            new FirebaseOptions
            {
                ApiKey = "test-api-key",
                ProjectId = "test-project"
            },
            new FakeAuthService(currentUserId),
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

    private static string CreateDriverRouteDocumentJson(int availableSeats = 2)
    {
        return $$"""
            {
              "name": "projects/test-project/databases/(default)/documents/weeklyRoutes/driver-route",
              "fields": {
                "userId": { "stringValue": "driver-user" },
                "role": { "stringValue": "driver" },
                "availableSeats": { "integerValue": "{{availableSeats}}" },
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

    private static string CreateRideRequestDocumentJson(
        string requestId,
        string passengerUserId,
        decimal suggestedPrice)
    {
        return $$"""
            {
              "name": "projects/test-project/databases/(default)/documents/rideRequests/{{requestId}}",
              "fields": {
                "passengerUserId": { "stringValue": "{{passengerUserId}}" },
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
                "suggestedPrice": { "doubleValue": {{suggestedPrice.ToString(System.Globalization.CultureInfo.InvariantCulture)}} },
                "createdAtUtc": { "timestampValue": "2026-09-03T12:00:00Z" }
              },
              "updateTime": "2026-09-03T13:00:00Z"
            }
            """;
    }

    private static string CreatePendingRouteRequestsQueryJson()
    {
        return $"[{{\"document\":{CreateRideRequestDocumentJson("request-1", "passenger-user", 3.45m)}}},"
            + $"{{\"document\":{CreateRideRequestDocumentJson("request-2", "other-passenger", 7.89m)}}}]";
    }

    private static string? GetWrittenStringField(
        JsonElement write,
        string fieldName)
    {
        return write.GetProperty("update")
            .GetProperty("fields")
            .GetProperty(fieldName)
            .GetProperty("stringValue")
            .GetString();
    }

    private static string? GetWrittenIntegerField(
        JsonElement write,
        string fieldName)
    {
        return write.GetProperty("update")
            .GetProperty("fields")
            .GetProperty(fieldName)
            .GetProperty("integerValue")
            .GetString();
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
        public FakeAuthService(string currentUserId)
        {
            CurrentUser = new User
            {
                Id = currentUserId,
                Name = currentUserId == "driver-user"
                    ? "Motorista"
                    : "Passageiro",
                Email = $"{currentUserId}@facens.br"
            };
        }

        public User? CurrentUser { get; }

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
