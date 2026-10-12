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

    Task ReauthenticateAsync(string currentPassword, CancellationToken cancellationToken = default);

    PendingEmailChange? PendingEmailChange { get; }
    void ClearReauthentication();
    Task BeginEmailChangeAsync(string newEmail, CancellationToken cancellationToken = default);
    Task ResendEmailConfirmationAsync(CancellationToken cancellationToken = default);
    Task<EmailConfirmationResult> CheckEmailConfirmationAsync(
        EmailConfirmationAction action, CancellationToken cancellationToken = default);
    Task RecoverEmailChangeSessionAsync(string password, CancellationToken cancellationToken = default);

    Task LogoutAsync();
}
