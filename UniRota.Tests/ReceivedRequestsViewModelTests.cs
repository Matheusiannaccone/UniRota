using UniRota.Models;
using UniRota.Services.Interfaces;
using UniRota.ViewModels;

namespace UniRota.Tests;

public sealed class ReceivedRequestsViewModelTests
{
    private static readonly DateOnly Today = new(2026, 10, 19);

    [Theory]
    [InlineData(RideRequestType.Weekly, "partial", true)]
    [InlineData(RideRequestType.Weekly, "full", false)]
    [InlineData(RideRequestType.Weekly, "once-today", false)]
    [InlineData(RideRequestType.Weekly, "once-future", false)]
    [InlineData(RideRequestType.Weekly, "once-past", true)]
    [InlineData(RideRequestType.Once, "available", true)]
    [InlineData(RideRequestType.Once, "full", false)]
    [InlineData(RideRequestType.Once, "once-today", false)]
    [InlineData(RideRequestType.Once, "once-future", true)]
    [InlineData(RideRequestType.Once, "past", false)]
    public async Task Load_UsesDomainAvailabilityAndKeepsRejectionAvailable(
        RideRequestType type, string scenario, bool expected)
    {
        var reservations = scenario.StartsWith("once-", StringComparison.Ordinal)
            ? new[] { Reservation(scenario == "once-past" ? Today.AddDays(-7)
                : scenario == "once-future" ? Today.AddDays(7) : Today) }
            : [];
        var weekly = new Dictionary<DayOfWeek, int>
        {
            [DayOfWeek.Monday] = scenario is "full" or "partial" ? 0 : 1,
            [DayOfWeek.Tuesday] = scenario == "partial" ? 1 : 0
        };
        var request = Request(type, scenario == "past" ? Today.AddDays(-7) : Today);
        var service = new RequestService([request]);
        var routes = new RouteService([Route(weekly, reservations)]);
        var vm = new ReceivedRequestsViewModel(service, routes, new Clock());

        await vm.LoadCommand.ExecuteAsync(null);

        Assert.False(vm.HasError);
        var item = Assert.Single(vm.Requests);
        Assert.Equal(expected, item.CanAccept);
        Assert.Equal(expected, vm.AcceptCommand.CanExecute(item));
        Assert.True(vm.RejectCommand.CanExecute(item));
        Assert.Equal(1, routes.Reads);
        Assert.Equal(0, service.Rejects);
        if (!expected)
        {
            await vm.AcceptCommand.ExecuteAsync(item);
            Assert.Equal(0, service.Accepts);
            Assert.Single(vm.Requests);
            await vm.RejectCommand.ExecuteAsync(item);
            Assert.Equal(1, service.Rejects);
            Assert.Empty(vm.Requests);
        }
        else
        {
            await vm.AcceptCommand.ExecuteAsync(item);
            Assert.Equal(1, service.Accepts);
            Assert.Empty(vm.Requests);
        }
    }

    [Theory]
    [InlineData(RideRequestStatus.Accepted)]
    [InlineData(RideRequestStatus.Rejected)]
    public async Task ProcessedRequestsCannotBeAcceptedAgain(RideRequestStatus status)
    {
        var request = Request(status: status);
        var service = new RequestService([request]);
        var vm = new ReceivedRequestsViewModel(service,
            new RouteService([Route(new Dictionary<DayOfWeek, int> { [DayOfWeek.Monday] = 1 })]), new Clock());

        await vm.LoadCommand.ExecuteAsync(null);
        var item = Assert.Single(vm.Requests);
        Assert.False(item.CanAccept);
        await vm.AcceptCommand.ExecuteAsync(item);
        Assert.Equal(0, service.Accepts);
    }

    [Fact]
    public async Task Load_ReadsRoutesOnceForSeveralRequestsAndDoesNotQueryOnPropertyAccess()
    {
        var service = new RequestService([Request(), Request(RideRequestType.Once, Today)]);
        var routes = new RouteService([Route(new Dictionary<DayOfWeek, int> { [DayOfWeek.Monday] = 2 })]);
        var vm = new ReceivedRequestsViewModel(service, routes, new Clock());

        await vm.LoadCommand.ExecuteAsync(null);
        Assert.Equal(2, vm.Requests.Count);
        Assert.All(vm.Requests, item => Assert.True(item.CanAccept));
        Assert.All(vm.Requests, item => Assert.True(vm.AcceptCommand.CanExecute(item)));
        Assert.Equal(1, routes.Reads);
    }

    [Fact]
    public async Task FailedAcceptanceRefreshesAvailabilityWithoutRejectingPending()
    {
        var weekly = new Dictionary<DayOfWeek, int> { [DayOfWeek.Monday] = 1 };
        var service = new RequestService([Request()])
        {
            BeforeAccept = () =>
            {
                weekly[DayOfWeek.Monday] = 0;
                throw new InvalidOperationException("Não há vagas.");
            }
        };
        var routes = new RouteService([Route(weekly)]);
        var vm = new ReceivedRequestsViewModel(service, routes, new Clock());
        await vm.LoadCommand.ExecuteAsync(null);

        await vm.AcceptCommand.ExecuteAsync(Assert.Single(vm.Requests));

        Assert.True(vm.HasError);
        Assert.False(Assert.Single(vm.Requests).CanAccept);
        Assert.Equal(2, routes.Reads);
        Assert.Equal(0, service.Rejects);
    }

    [Fact]
    public async Task MissingRouteDisablesAcceptAndEmptyListSkipsRouteQuery()
    {
        var service = new RequestService([Request()]);
        var routes = new RouteService([]);
        var vm = new ReceivedRequestsViewModel(service, routes, new Clock());
        await vm.LoadCommand.ExecuteAsync(null);
        Assert.False(Assert.Single(vm.Requests).CanAccept);
        service.Requests = [];
        await vm.LoadCommand.ExecuteAsync(null);
        Assert.Empty(vm.Requests);
        Assert.Equal(1, routes.Reads);
    }

    private static RideRequest Request(RideRequestType type = RideRequestType.Weekly,
        DateOnly? date = null, RideRequestStatus status = RideRequestStatus.Pending) => new()
    {
        Id = type.ToString(), DriverRouteId = "route", DriverUserId = "driver", Type = type,
        Status = status, CompatibleDays = [DayOfWeek.Monday, DayOfWeek.Tuesday],
        RequestedDate = type == RideRequestType.Once ? date : null
    };

    private static WeeklyRoute Route(IReadOnlyDictionary<DayOfWeek, int> weekly,
        IReadOnlyList<OneTimeReservation>? reservations = null) => new()
    {
        Id = "route", UserId = "driver", Role = RouteRole.Driver, SeatCapacity = 2,
        DaysOfWeek = weekly.Keys.ToArray(), WeeklyAvailableSeats = weekly,
        OneTimeReservations = reservations ?? []
    };

    private static OneTimeReservation Reservation(DateOnly date) => new()
    {
        RequestId = "existing", Date = date, DayOfWeek = date.DayOfWeek
    };

    private sealed class Clock : IAppClock { public DateOnly Today => ReceivedRequestsViewModelTests.Today; }

    private sealed class RouteService(IReadOnlyList<WeeklyRoute> routes) : IRouteService
    {
        public int Reads { get; private set; }
        public Task<IReadOnlyList<WeeklyRoute>> GetMyRoutesAsync(CancellationToken cancellationToken = default)
        { Reads++; return Task.FromResult(routes); }
        public Task<WeeklyRoute> CreateAsync(WeeklyRoute route, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<WeeklyRoute> UpdateAsync(WeeklyRoute route, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task DeleteAsync(string routeId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<WeeklyRoute>> GetDriverRoutesAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class RequestService(IReadOnlyList<RideRequest> requests) : IRideRequestService
    {
        public IReadOnlyList<RideRequest> Requests { get; set; } = requests;
        public int Accepts { get; private set; }
        public int Rejects { get; private set; }
        public Action? BeforeAccept { get; init; }
        public Task<IReadOnlyList<RideRequest>> GetReceivedPendingRequestsAsync(CancellationToken cancellationToken = default) => Task.FromResult(Requests);
        public Task AcceptAsync(string requestId, CancellationToken cancellationToken = default)
        { Accepts++; BeforeAccept?.Invoke(); Requests = Requests.Where(request => request.Id != requestId).ToArray(); return Task.CompletedTask; }
        public Task RejectAsync(string requestId, CancellationToken cancellationToken = default)
        { Rejects++; Requests = Requests.Where(request => request.Id != requestId).ToArray(); return Task.CompletedTask; }
        public Task<RideRequest> CreateAsync(string passengerRouteId, MatchResult match, RideRequestType type,
            DateOnly? requestedDate, decimal suggestedPrice, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> HasPendingRequestAsync(string passengerRouteId, string driverRouteId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<RideRequest>> GetMyPendingRequestsAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<RideRequest>> GetMyActiveRequestsAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<RideRequest>> GetMyAcceptedRequestsAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
