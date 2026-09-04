using UniRota.Services;

namespace UniRota.Tests;

public sealed class PricingServiceTests
{
    private readonly PricingService _service = new();

    [Fact]
    public void Calculate_ReturnsExpectedValues_ForBasicCalculation()
    {
        var result = _service.Calculate(10m);

        Assert.Equal(10m, result.DistanceKm);
        Assert.Equal(5.30m, result.EstimatedTripCost);
        Assert.Equal(2.65m, result.SuggestedPrice);
    }

    [Fact]
    public void Calculate_AcceptsDecimalDistance()
    {
        var result = _service.Calculate(7.5m);

        Assert.Equal(7.5m, result.DistanceKm);
        Assert.Equal(3.975m, result.EstimatedTripCost);
        Assert.Equal(1.99m, result.SuggestedPrice);
    }

    [Fact]
    public void Calculate_RoundsSuggestedPriceToTwoDecimalPlaces()
    {
        var result = _service.Calculate(12.34m);

        Assert.Equal(3.27m, result.SuggestedPrice);
        Assert.Equal(
            result.SuggestedPrice,
            decimal.Round(result.SuggestedPrice, 2));
    }

    [Fact]
    public void Calculate_UsesAwayFromZeroForMidpointRounding()
    {
        var result = _service.Calculate(1m);

        Assert.Equal(0.265m, result.EstimatedTripCost / 2m);
        Assert.Equal(0.27m, result.SuggestedPrice);
    }

    [Fact]
    public void Calculate_ThrowsWhenDistanceIsZero()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => _service.Calculate(0m));
    }

    [Fact]
    public void Calculate_ThrowsWhenDistanceIsNegative()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => _service.Calculate(-1m));
    }

    [Fact]
    public void Calculate_ReturnsFixedCostPerKm()
    {
        var result = _service.Calculate(10m);

        Assert.Equal(0.53m, result.CostPerKm);
    }

    [Fact]
    public void Calculate_DoesNotRoundEstimatedTripCostBeforeSuggestedPrice()
    {
        var result = _service.Calculate(1.03m);

        Assert.Equal(0.5459m, result.EstimatedTripCost);
        Assert.Equal(0.27m, result.SuggestedPrice);
        Assert.NotEqual(0.28m, result.SuggestedPrice);
    }
}
