using UniRota.Models;
using UniRota.Services.Interfaces;

namespace UniRota.Services;

public sealed class PricingService : IPricingService
{
    private const decimal CostPerKm = 0.53m;
    private const decimal ParticipantCount = 2m;

    public PricingResult Calculate(decimal distanceKm)
    {
        if (distanceKm <= 0m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(distanceKm),
                "A distância deve ser maior que zero.");
        }

        var estimatedTripCost = distanceKm * CostPerKm;
        var suggestedPrice = Math.Round(
            estimatedTripCost / ParticipantCount,
            2,
            MidpointRounding.AwayFromZero);

        return new PricingResult(
            distanceKm,
            CostPerKm,
            estimatedTripCost,
            suggestedPrice);
    }
}
