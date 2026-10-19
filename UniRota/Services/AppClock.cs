using UniRota.Services.Interfaces;

namespace UniRota.Services;

// Referência local para validações antecipadas; autoridade de segurança temporal nas Rules (Bloco 7).
public sealed class AppClock(TimeProvider timeProvider) : IAppClock
{
    private static readonly TimeZoneInfo AppTimeZone =
        TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");

    public DateOnly Today => DateOnly.FromDateTime(
        TimeZoneInfo.ConvertTime(timeProvider.GetUtcNow(), AppTimeZone).DateTime);
}
