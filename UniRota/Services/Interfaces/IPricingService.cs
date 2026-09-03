using UniRota.Models;

namespace UniRota.Services.Interfaces;

public interface IPricingService
{
    PricingResult Calculate(decimal distanceKm);
}
