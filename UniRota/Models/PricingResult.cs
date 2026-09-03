namespace UniRota.Models;

public sealed record PricingResult(
    decimal DistanceKm,
    decimal CostPerKm,
    decimal EstimatedTripCost,
    decimal SuggestedPrice);
