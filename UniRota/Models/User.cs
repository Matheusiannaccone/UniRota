namespace UniRota.Models;

public sealed class User
{
    public string Id { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public string Email { get; init; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; init; }
}
