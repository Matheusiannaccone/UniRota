using System.Net.Mail;
using System.Text.Json;
using UniRota.Models;

namespace UniRota.Services.Firebase;

public sealed partial class FirebaseAuthService
{
    private const string PendingEmailStorageKey = "firebase_pending_email_change";
    private readonly SemaphoreSlim _emailChangeLock = new(1, 1);
    public PendingEmailChange? PendingEmailChange { get; private set; }

    public void ClearReauthentication() => _reauthenticatedUntilUtc = default;

    public async Task ChangePasswordAsync(string newPassword, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 6)
            throw new ArgumentException("A senha deve ter pelo menos 6 caracteres.");
        if (PendingEmailChange is not null)
            throw new InvalidOperationException("Conclua ou cancele a alteração de e-mail antes de alterar sua senha.");
        var user = CurrentUser ?? throw CreateFirebaseException("INVALID_ID_TOKEN");
        var token = await GetValidIdTokenAsync(cancellationToken);
        await _tokenRefreshLock.WaitAsync(cancellationToken);
        var changed = false;
        try
        {
            EnsureCurrentUser(user);
            EnsureRecentAuthentication();
            var response = await PostJsonAsync<FirebaseAuthResponseDto>(
                $"{IdentityToolkitBaseUrl}/accounts:update?key={Escape(_options.ApiKey)}",
                new { idToken = token, password = newPassword, returnSecureToken = true }, cancellationToken);
            changed = true;
            EnsureCurrentUser(user);
            if (response.LocalId != user.Id) throw CreateFirebaseException("INVALID_ID_TOKEN");
            await SetCurrentSessionAsync(user,
                RequireResponseValue(response.IdToken, "ID token"),
                RequireResponseValue(response.RefreshToken, "refresh token"),
                GetIdTokenExpiration(response.ExpiresIn), cancellationToken);
        }
        catch (Exception ex) when (changed)
        {
            try { ClearLocalSession(); } catch { ClearSessionInMemory(); }
            throw new InvalidOperationException("A senha foi alterada, mas não foi possível manter a sessão. Entre novamente com a nova senha.", ex);
        }
        finally
        {
            ClearReauthentication();
            _tokenRefreshLock.Release();
        }
    }

    public async Task BeginEmailChangeAsync(string newEmail, CancellationToken cancellationToken = default)
    {
        newEmail = newEmail.Trim();
        if (!MailAddress.TryCreate(newEmail, out var address) || address.Address != newEmail)
            throw new ArgumentException("Informe um e-mail válido.");

        await _emailChangeLock.WaitAsync(cancellationToken);
        try
        {
            if (PendingEmailChange is not null)
                throw new InvalidOperationException("Já existe uma alteração de e-mail em andamento.");
            var user = CurrentUser ?? throw CreateFirebaseException("INVALID_ID_TOKEN");
            if (string.Equals(user.Email, newEmail, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Informe um e-mail diferente do atual.");
            EnsureRecentAuthentication();
            var token = await GetValidIdTokenAsync(cancellationToken);
            EnsureCurrentUser(user);
            // Reservar antes do envio impede reenvios extras se a resposta se perder ou o app fechar.
            await SavePendingEmailChangeAsync(new(user.Id, user.Email, newEmail, 0, _timeProvider.GetUtcNow().AddSeconds(60)));
            try { await SendEmailLinkAsync(token, newEmail, cancellationToken); }
            catch (InvalidOperationException ex) when (ex.Data.Contains("FirebaseCode"))
            {
                ClearPendingEmailChange(); // Firebase recusou explicitamente: nenhum envio aceito.
                throw;
            }
            finally
            {
                ClearReauthentication();
                if (PendingEmailChange is not null) await RestartEmailCooldownAsync();
            }
        }
        finally { _emailChangeLock.Release(); }
    }

    public async Task ResendEmailConfirmationAsync(CancellationToken cancellationToken = default)
    {
        await _emailChangeLock.WaitAsync(cancellationToken);
        try
        {
            var pending = RequirePendingEmailChange();
            EnsureEmailCooldownElapsed(pending);
            if (pending.ResendCount >= 2)
                throw new InvalidOperationException("O limite de dois reenvios foi atingido.");
            var token = await GetValidIdTokenAsync(cancellationToken);
            // Nunca reenviar se o endereço já foi efetivado; a próxima consulta concluirá a sincronização.
            var account = await LookupAccountAsync(token, pending.UserId, cancellationToken);
            if (!string.Equals(account.Email, pending.OriginalEmail, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("O e-mail da conta mudou. Use Verificar confirmação para sincronizar.");
            await SavePendingEmailChangeAsync(pending with { ResendCount = pending.ResendCount + 1 });
            try { await SendEmailLinkAsync(token, pending.NewEmail, cancellationToken); }
            catch (InvalidOperationException ex) when (ex.Data.Contains("FirebaseCode"))
            {
                await SavePendingEmailChangeAsync(pending);
                throw;
            }
            finally { await RestartEmailCooldownAsync(); }
        }
        finally { _emailChangeLock.Release(); }
    }

    public async Task<EmailConfirmationResult> CheckEmailConfirmationAsync(
        EmailConfirmationAction action, CancellationToken cancellationToken = default)
    {
        await _emailChangeLock.WaitAsync(cancellationToken);
        try
        {
            var pending = RequirePendingEmailChange();
            if (action == EmailConfirmationAction.Manual) EnsureEmailCooldownElapsed(pending);
            try
            {
                var token = await GetValidIdTokenAsync(cancellationToken);
                var account = await LookupAccountAsync(token, pending.UserId, cancellationToken);
                if (string.Equals(account.Email, pending.NewEmail, StringComparison.OrdinalIgnoreCase))
                {
                    var user = CurrentUser ?? throw CreateFirebaseException("INVALID_ID_TOKEN");
                    await _tokenRefreshLock.WaitAsync(cancellationToken);
                    try
                    {
                        EnsureCurrentUser(user);
                        var updated = await SynchronizeEmailAsync(user, account.Email!, token, cancellationToken);
                        EnsureCurrentUser(user);
                        CurrentUser = updated;
                        ClearPendingEmailChange();
                    }
                    finally { _tokenRefreshLock.Release(); }
                    return EmailConfirmationResult.Confirmed;
                }
                if (!string.Equals(account.Email, pending.OriginalEmail, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("A conta está usando outro e-mail. A sincronização precisa ser revisada; nenhum dado local foi alterado.");
                if (action == EmailConfirmationAction.Cancel)
                {
                    ClearPendingEmailChange();
                    return EmailConfirmationResult.Cancelled;
                }
                return EmailConfirmationResult.Waiting;
            }
            finally
            {
                if (PendingEmailChange is not null && action != EmailConfirmationAction.Cancel)
                    await RestartEmailCooldownAsync();
            }
        }
        finally { _emailChangeLock.Release(); }
    }

    public async Task RecoverEmailChangeSessionAsync(string password, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(password)) throw new ArgumentException("Informe sua senha atual.");
        await _emailChangeLock.WaitAsync(cancellationToken);
        try
        {
            var pending = RequirePendingEmailChange();
            FirebaseAuthResponseDto response;
            try
            {
                try { response = await SignInForRecoveryAsync(pending.NewEmail, password, cancellationToken); }
                catch (InvalidOperationException ex) when (IsInvalidCredentialsException(ex))
                {
                    response = await SignInForRecoveryAsync(pending.OriginalEmail, password, cancellationToken);
                }
            }
            catch (InvalidOperationException ex) when (IsInvalidCredentialsException(ex))
            {
                throw CreateInvalidProfilePasswordException(ex);
            }
            if (response.LocalId != pending.UserId)
                throw new InvalidOperationException("A senha informada não autenticou a conta deste fluxo.");
            var token = RequireResponseValue(response.IdToken, "ID token");
            var user = CurrentUser ?? await GetUserProfileAsync(pending.UserId, token, cancellationToken);
            if (user.Id != pending.UserId) throw CreateFirebaseException("INVALID_ID_TOKEN");
            await SetCurrentSessionAsync(user, token,
                RequireResponseValue(response.RefreshToken, "refresh token"),
                GetIdTokenExpiration(response.ExpiresIn), cancellationToken);
        }
        finally { _emailChangeLock.Release(); }
    }

    private Task<FirebaseAuthResponseDto> SignInForRecoveryAsync(string email, string password, CancellationToken ct) =>
        PostJsonAsync<FirebaseAuthResponseDto>($"{IdentityToolkitBaseUrl}/accounts:signInWithPassword?key={Escape(_options.ApiKey)}",
            new { email, password, returnSecureToken = true }, ct);

    private async Task SendEmailLinkAsync(string token, string newEmail, CancellationToken ct)
    {
        using var request = CreateJsonRequest(HttpMethod.Post,
            $"{IdentityToolkitBaseUrl}/accounts:sendOobCode?key={Escape(_options.ApiKey)}",
            new { requestType = "VERIFY_AND_CHANGE_EMAIL", idToken = token, newEmail });
        request.Headers.Add("X-Firebase-Locale", "pt-BR");
        EnsureSuccess(await SendAsync(request, ct));
    }

    private async Task<FirebaseAuthResponseDto> LookupAccountAsync(string token, string userId, CancellationToken ct)
    {
        var response = await PostJsonAsync<AccountLookupDto>($"{IdentityToolkitBaseUrl}/accounts:lookup?key={Escape(_options.ApiKey)}",
            new { idToken = token }, ct);
        var account = response.Users.SingleOrDefault();
        if (account?.LocalId != userId || string.IsNullOrWhiteSpace(account.Email))
            throw CreateFirebaseException("INVALID_ID_TOKEN");
        return account;
    }

    private async Task<User> SynchronizeEmailAsync(User user, string email, string token, CancellationToken ct)
    {
        if (string.Equals(user.Email, email, StringComparison.Ordinal)) return user;
        try { await UpdateProfileFieldAsync(user.Id, "email", email, token, ct); }
        catch (Exception ex)
        {
            var failure = new InvalidOperationException(
                "O e-mail já foi alterado no Firebase Authentication, mas o perfil ainda não foi sincronizado. Tente verificar novamente. Se o acesso for negado, as regras de users precisam ser revisadas.", ex);
            failure.Data["FirebaseCode"] = ex.Data["FirebaseCode"];
            throw failure;
        }
        return new User { Id = user.Id, Name = user.Name, Email = email, CreatedAtUtc = user.CreatedAtUtc };
    }

    private PendingEmailChange RequirePendingEmailChange() => PendingEmailChange
        ?? throw new InvalidOperationException("Não há alteração de e-mail pendente.");

    private void EnsureEmailCooldownElapsed(PendingEmailChange pending)
    {
        if (_timeProvider.GetUtcNow() < pending.ActionsAvailableAtUtc)
            throw new InvalidOperationException("Aguarde o temporizador antes de tentar novamente.");
    }

    private Task RestartEmailCooldownAsync() => SavePendingEmailChangeAsync(
        RequirePendingEmailChange() with { ActionsAvailableAtUtc = _timeProvider.GetUtcNow().AddSeconds(60) });

    private async Task SavePendingEmailChangeAsync(PendingEmailChange pending)
    {
        await _secureStorage.SetAsync(PendingEmailStorageKey, JsonSerializer.Serialize(pending, JsonOptions));
        PendingEmailChange = pending;
    }

    private async Task LoadPendingEmailChangeAsync()
    {
        if (PendingEmailChange is not null) return;
        var json = await _secureStorage.GetAsync(PendingEmailStorageKey);
        if (!string.IsNullOrWhiteSpace(json))
            PendingEmailChange = DeserializeResponse<PendingEmailChange>(json);
    }

    private void ClearPendingEmailChange()
    {
        _secureStorage.Remove(PendingEmailStorageKey);
        PendingEmailChange = null;
        ClearReauthentication();
    }

    private sealed class AccountLookupDto
    {
        public List<FirebaseAuthResponseDto> Users { get; init; } = [];
    }
}
