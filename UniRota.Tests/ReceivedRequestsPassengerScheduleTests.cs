using UniRota.Models;

namespace UniRota.Tests;

public sealed partial class ReceivedRequestsViewModelTests
{
    [Theory]
    [InlineData("weekly", "O passageiro já possui uma carona semanal confirmada para este dia.")]
    [InlineData("once", "O passageiro já possui uma carona confirmada para esta data.")]
    [InlineData("blocked-weekly", "Os dias dessa solicitação possuem conflito com caronas já confirmadas do passageiro.")]
    public async Task PassengerScheduleConflict_ShowsExactReasonWithoutAcceptOrReject(string scenario, string message)
    {
        var service = new RequestService([Request(scenario == "blocked-weekly" ? RideRequestType.Weekly : RideRequestType.Once, Today)])
        {
            PassengerAcceptedDays = scenario == "weekly" ? [DayOfWeek.Monday] : [],
            PassengerAcceptedDates = scenario == "blocked-weekly" ? [Today, Today.AddDays(1)] : [Today]
        };
        var vm = new UniRota.ViewModels.ReceivedRequestsViewModel(service,
            new RouteService([Route(new Dictionary<DayOfWeek, int> { [DayOfWeek.Monday] = 2, [DayOfWeek.Tuesday] = 2 })]), new Clock());
        await vm.LoadCommand.ExecuteAsync(null);
        var item = Assert.Single(vm.Requests);
        Assert.True(item.CanAccept);
        Assert.Equal(message, item.PassengerConflictMessage);

        await vm.ConfirmAndAcceptAsync(item, _ => throw new InvalidOperationException("Não deve confirmar conflito."));

        Assert.Equal(message, vm.ErrorMessage);
        Assert.Equal(0, service.Accepts);
        Assert.Equal(0, service.Rejects);
        Assert.Equal(RideRequestStatus.Pending, Assert.Single(vm.Requests).Request.Status);
        Assert.True(vm.RejectCommand.CanExecute(item));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PassengerOnceMakesWeeklyPartial_ConfirmsExactlyRemainingDays(bool confirm)
    {
        var service = new RequestService([Request()]) { PassengerAcceptedDates = [Today.AddDays(7)] };
        var vm = new UniRota.ViewModels.ReceivedRequestsViewModel(service,
            new RouteService([Route(new Dictionary<DayOfWeek, int> { [DayOfWeek.Monday] = 2, [DayOfWeek.Tuesday] = 2 })]), new Clock());
        await vm.LoadCommand.ExecuteAsync(null);
        var item = Assert.Single(vm.Requests);
        Assert.Equal(new[] { DayOfWeek.Tuesday }, item.AcceptableDays);
        string? shown = null;

        await vm.ConfirmAndAcceptAsync(item, message => { shown = message; return Task.FromResult(confirm); });

        Assert.Equal("Os dias ainda pendentes para serem aceitos são: Terça-feira. Deseja aceitar a solicitação somente nesses dias?", shown);
        Assert.Equal(confirm ? 1 : 0, service.Accepts);
        if (confirm) Assert.Equal(new[] { DayOfWeek.Tuesday }, service.LastConfirmedDays);
        else Assert.Single(vm.Requests);
    }
}
