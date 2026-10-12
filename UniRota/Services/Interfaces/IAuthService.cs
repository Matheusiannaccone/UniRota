using UniRota.Models;

namespace UniRota.Services.Interfaces;

public interface IAuthService
{
    User? CurrentUser { get; }

    Task<User> RegisterAsync(
        string name,
        string email,
        string password,
        CancellationToken cancellationToken = default);

    Task<User> LoginAsync(
        string email,
        string password,
        CancellationToken cancellationToken = default);

    Task<User?> RestoreSessionAsync(CancellationToken cancellationToken = default);

    Task<string> GetValidIdTokenAsync(CancellationToken cancellationToken = default);

    Task UpdateNameAsync(string name, CancellationToken cancellationToken = default);

    Task LogoutAsync();
}
