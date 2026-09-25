using UniRota.Models;
using UniRota.ViewModels;

namespace UniRota.Tests;

public sealed class ConfirmedRideFilterTests
{
    [Fact]
    public void Segments_KeepPastOnceRidesAccessibleAndPreservePriceSnapshots()
    {
        // Apenas projeção local: nenhuma operação de autenticação/rede é executada.
        var viewModel = new ConfirmedRoutesViewModel(null!, null!);
        var once = new ConfirmedRideItemViewModel(new RideRequest
        {
            PassengerUserId = "user",
            Type = RideRequestType.Once,
            Status = RideRequestStatus.Accepted,
            RequestedDate = new DateOnly(2020, 1, 1),
            SuggestedPrice = 7.25m
        }, "user");
        var weekly = new ConfirmedRideItemViewModel(new RideRequest
        {
            PassengerUserId = "user",
            Type = RideRequestType.Weekly,
            Status = RideRequestStatus.Accepted,
            SuggestedPrice = 9.50m
        }, "user");
        viewModel.Routes.Add(once);
        viewModel.Routes.Add(weekly);

        Assert.Same(once, Assert.Single(viewModel.VisibleRoutes));
        viewModel.ShowWeeklyRidesCommand.Execute(null);
        Assert.Same(weekly, Assert.Single(viewModel.VisibleRoutes));
        viewModel.ShowOnceRidesCommand.Execute(null);
        Assert.Same(once, Assert.Single(viewModel.VisibleRoutes));
        Assert.Equal(2, viewModel.Routes.Count);
        Assert.Equal(7.25m, once.SuggestedPrice);
        Assert.Equal(9.50m, weekly.SuggestedPrice);
    }

    [Fact]
    public void ReloadedCollection_UpdatesSelectedSegmentAndEmptyState()
    {
        var viewModel = new ConfirmedRoutesViewModel(null!, null!);
        viewModel.ShowWeeklyRidesCommand.Execute(null);
        var notifications = new List<string?>();
        viewModel.PropertyChanged += (_, args) => notifications.Add(args.PropertyName);
        viewModel.Routes.Add(new ConfirmedRideItemViewModel(new RideRequest
        {
            DriverUserId = "user", Type = RideRequestType.Weekly
        }, "user"));

        Assert.False(viewModel.ShowEmptyState);
        Assert.Contains(nameof(viewModel.VisibleRoutes), notifications);
        viewModel.Routes.Clear();
        Assert.True(viewModel.ShowEmptyState);
        Assert.True(viewModel.ShowRecurring);
        Assert.Contains("recorrentes", viewModel.EmptyStateText);
        viewModel.IsBusy = true;
        Assert.False(viewModel.ShowEmptyState);
        viewModel.IsBusy = false;
        viewModel.HasError = true;
        Assert.False(viewModel.ShowEmptyState);
    }
}
