using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Maui.Storage;
using UniRota.Models;
using UniRota.Services.Interfaces;

namespace UniRota.Services.Firebase;

public sealed class FirebaseAuthService : IAuthService
{
    private const string RefreshTokenStorageKey = "firebase_refresh_token";
    private const string IdentityToolkitBaseUrl = "https://identitytoolkit.googleapis.com/v1";
    private const string SecureTokenBaseUrl = "https://securetoken.googleapis.com/v1";
    private const string FirestoreBaseUrl = "https://firestore.googleapis.com/v1";

    private static readonly TimeSpan TokenRefreshMargin = TimeSpan.FromMinutes(2);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;
    private readonly ISecureStorage _secureStorage;
    private readonly FirebaseOptions _options;
    private readonly SemaphoreSlim _tokenRefreshLock = new(1, 1);
    private string? _idToken;
    private string? _refreshToken;
    private DateTimeOffset _idTokenExpiresAtUtc;

    public FirebaseAuthService(
        HttpClient httpClient,
        ISecureStorage secureStorage,
        FirebaseOptions options)
    {
        _httpClient = httpClient;
        _secureStorage = secureStorage;
        _options = options;
    }

    public User? CurrentUser { get; private set; }

    public async Task<User> RegisterAsync(
        string name,
        string email,
        string password,
        CancellationToken cancellationToken = default)
    {
        EnsureFirebaseIsConfigured();
        ValidateRequiredValue(name, nameof(name));
        ValidateRequiredValue(email, nameof(email));
        ValidateRequiredValue(password, nameof(password));

        var requestBody = new
        {
            email = email.Trim(),
            password,
            returnSecureToken = true
        };

        var authResponse = await PostJsonAsync<FirebaseAuthResponseDto>(
            $"{IdentityToolkitBaseUrl}/accounts:signUp?key={Escape(_options.ApiKey)}",
            requestBody,
            cancellationToken);

        var user = new User
        {
            Id = RequireResponseValue(authResponse.LocalId, "identificador do usuário"),
            Name = name.Trim(),
            Email = RequireResponseValue(authResponse.Email, "e-mail do usuário"),
            CreatedAtUtc = DateTimeOffset.UtcNow
        };

        var idToken = RequireResponseValue(authResponse.IdToken, "ID token");
        var refreshToken = RequireResponseValue(authResponse.RefreshToken, "refresh token");
        var idTokenExpiresAtUtc = GetIdTokenExpiration(authResponse.ExpiresIn);

        await CreateUserProfileAsync(user, idToken, cancellationToken);
        await SetCurrentSessionAsync(
            user,
            idToken,
            refreshToken,
            idTokenExpiresAtUtc,
            cancellationToken);

        return user;
    }

    public async Task<User> LoginAsync(
        string email,
        string password,
        CancellationToken cancellationToken = default)
    {
        EnsureFirebaseIsConfigured();
        ValidateRequiredValue(email, nameof(email));
        ValidateRequiredValue(password, nameof(password));

        var requestBody = new
        {
            email = email.Trim(),
            password,
            returnSecureToken = true
        };

        var authResponse = await PostJsonAsync<FirebaseAuthResponseDto>(
            $"{IdentityToolkitBaseUrl}/accounts:signInWithPassword?key={Escape(_options.ApiKey)}",
            requestBody,
            cancellationToken);

        var idToken = RequireResponseValue(authResponse.IdToken, "ID token");
        var refreshToken = RequireResponseValue(authResponse.RefreshToken, "refresh token");
        var idTokenExpiresAtUtc = GetIdTokenExpiration(authResponse.ExpiresIn);
        var userId = RequireResponseValue(authResponse.LocalId, "identificador do usuário");
        var user = await GetUserProfileAsync(userId, idToken, cancellationToken);

        await SetCurrentSessionAsync(
            user,
            idToken,
            refreshToken,
            idTokenExpiresAtUtc,
            cancellationToken);

        return user;
    }

    public async Task<User?> RestoreSessionAsync(CancellationToken cancellationToken = default)
    {
        EnsureFirebaseIsConfigured();
        cancellationToken.ThrowIfCancellationRequested();

        var storedRefreshToken = await GetStoredRefreshTokenAsync();

        if (string.IsNullOrWhiteSpace(storedRefreshToken))
        {
            ClearSessionInMemory();
            return null;
        }

        var refreshedTokens = await RefreshIdTokenAsync(
            storedRefreshToken,
            returnNullForInvalidSession: true,
            cancellationToken);

        if (refreshedTokens is null)
        {
            return null;
        }

        var user = await GetUserProfileAsync(
            refreshedTokens.UserId,
            refreshedTokens.IdToken,
            cancellationToken);

        await SetCurrentSessionAsync(
            user,
            refreshedTokens.IdToken,
            refreshedTokens.RefreshToken,
            refreshedTokens.ExpiresAtUtc,
            cancellationToken);

        return user;
    }

    public async Task<string> GetValidIdTokenAsync(
        CancellationToken cancellationToken = default)
    {
        EnsureFirebaseIsConfigured();
        cancellationToken.ThrowIfCancellationRequested();

        var currentUser = CurrentUser
            ?? throw new InvalidOperationException(
                "Não há uma sessão autenticada. Entre novamente para continuar.");

        if (HasValidIdToken())
        {
            return _idToken!;
        }

        await _tokenRefreshLock.WaitAsync(cancellationToken);

        try
        {
            if (CurrentUser?.Id != currentUser.Id)
            {
                throw new InvalidOperationException(
                    "A sessão autenticada foi alterada. Entre novamente para continuar.");
            }

            if (HasValidIdToken())
            {
                return _idToken!;
            }

            var refreshToken = _refreshToken;

            if (string.IsNullOrWhiteSpace(refreshToken))
            {
                refreshToken = await GetStoredRefreshTokenAsync();
            }

            if (string.IsNullOrWhiteSpace(refreshToken))
            {
                ClearSessionInMemory();
                throw new InvalidOperationException(
                    "A sessão salva não está disponível. Entre novamente para continuar.");
            }

            var refreshedTokens = await RefreshIdTokenAsync(
                refreshToken,
                returnNullForInvalidSession: false,
                cancellationToken)
                ?? throw new InvalidOperationException(
                    "Não foi possível renovar a sessão autenticada.");

            if (!string.Equals(
                    refreshedTokens.UserId,
                    currentUser.Id,
                    StringComparison.Ordinal)
                || CurrentUser?.Id != currentUser.Id)
            {
                ClearLocalSession();
                throw new InvalidOperationException(
                    "A sessão autenticada não corresponde ao usuário atual. Entre novamente.");
            }

            await SetCurrentSessionAsync(
                currentUser,
                refreshedTokens.IdToken,
                refreshedTokens.RefreshToken,
                refreshedTokens.ExpiresAtUtc,
                cancellationToken);

            return refreshedTokens.IdToken;
        }
        finally
        {
            _tokenRefreshLock.Release();
        }
    }

    public Task LogoutAsync()
    {
        ClearLocalSession();
        return Task.CompletedTask;
    }

    private async Task CreateUserProfileAsync(
        User user,
        string idToken,
        CancellationToken cancellationToken)
    {
        var requestBody = new
        {
            fields = new
            {
                id = new { stringValue = user.Id },
                name = new { stringValue = user.Name },
                email = new { stringValue = user.Email },
                createdAtUtc = new { timestampValue = user.CreatedAtUtc.ToUniversalTime().ToString("O") }
            }
        };

        using var request = CreateJsonRequest(
            HttpMethod.Patch,
            BuildUserDocumentUrl(user.Id),
            requestBody);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", idToken);

        var response = await SendAsync(request, cancellationToken);
        EnsureSuccess(response);
    }

    private async Task<User> GetUserProfileAsync(
        string userId,
        string idToken,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, BuildUserDocumentUrl(userId));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", idToken);

        var response = await SendAsync(request, cancellationToken);
        EnsureSuccess(response);

        var document = DeserializeResponse<FirestoreDocumentDto>(response.Content);
        var fields = document.Fields;

        return new User
        {
            Id = GetRequiredStringField(fields, "id"),
            Name = GetRequiredStringField(fields, "name"),
            Email = GetRequiredStringField(fields, "email"),
            CreatedAtUtc = GetRequiredTimestampField(fields, "createdAtUtc")
        };
    }

    private async Task SetCurrentSessionAsync(
        User user,
        string idToken,
        string refreshToken,
        DateTimeOffset idTokenExpiresAtUtc,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            await _secureStorage.SetAsync(RefreshTokenStorageKey, refreshToken);
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException(
                "A autenticação foi concluída, mas não foi possível salvar a sessão no dispositivo.",
                exception);
        }

        cancellationToken.ThrowIfCancellationRequested();

        _idToken = idToken;
        _refreshToken = refreshToken;
        _idTokenExpiresAtUtc = idTokenExpiresAtUtc;
        CurrentUser = user;
    }

    private async Task<string?> GetStoredRefreshTokenAsync()
    {
        try
        {
            return await _secureStorage.GetAsync(RefreshTokenStorageKey);
        }
        catch (Exception exception)
        {
            ClearSessionInMemory();
            throw new InvalidOperationException(
                "Não foi possível acessar a sessão salva neste dispositivo.",
                exception);
        }
    }

    private async Task<RefreshedTokens?> RefreshIdTokenAsync(
        string refreshToken,
        bool returnNullForInvalidSession,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"{SecureTokenBaseUrl}/token?key={Escape(_options.ApiKey)}")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "refresh_token",
                ["refresh_token"] = refreshToken
            })
        };

        var response = await SendAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var firebaseCode = ExtractFirebaseErrorCode(response.Content);

            if (IsInvalidSessionError(firebaseCode))
            {
                ClearLocalSession();

                if (returnNullForInvalidSession)
                {
                    return null;
                }

                var invalidSessionException = new InvalidOperationException(
                    "Sua sessão não é mais válida. Entre novamente para continuar.");
                invalidSessionException.Data["FirebaseCode"] = firebaseCode;
                throw invalidSessionException;
            }

            throw CreateFirebaseException(firebaseCode, response.Content);
        }

        var tokenResponse = DeserializeResponse<RefreshTokenResponseDto>(response.Content);

        return new RefreshedTokens(
            RequireResponseValue(tokenResponse.UserId, "identificador do usuário"),
            RequireResponseValue(tokenResponse.IdToken, "ID token"),
            RequireResponseValue(tokenResponse.RefreshToken, "refresh token"),
            GetIdTokenExpiration(tokenResponse.ExpiresIn));
    }

    private bool HasValidIdToken()
    {
        return !string.IsNullOrWhiteSpace(_idToken)
            && DateTimeOffset.UtcNow.Add(TokenRefreshMargin) < _idTokenExpiresAtUtc;
    }

    private async Task<T> PostJsonAsync<T>(
        string url,
        object requestBody,
        CancellationToken cancellationToken)
    {
        using var request = CreateJsonRequest(HttpMethod.Post, url, requestBody);
        var response = await SendAsync(request, cancellationToken);
        EnsureSuccess(response);

        return DeserializeResponse<T>(response.Content);
    }

    private async Task<FirebaseHttpResponse> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        try
        {
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            return new FirebaseHttpResponse(response.IsSuccessStatusCode, content);
        }
        catch (HttpRequestException exception)
        {
            throw new InvalidOperationException(
                "Não foi possível conectar aos serviços do Firebase. Verifique sua conexão.",
                exception);
        }
    }

    private static HttpRequestMessage CreateJsonRequest(
        HttpMethod method,
        string url,
        object requestBody)
    {
        return new HttpRequestMessage(method, url)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(requestBody, JsonOptions),
                Encoding.UTF8,
                "application/json")
        };
    }

    private static void EnsureSuccess(FirebaseHttpResponse response)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var firebaseCode = ExtractFirebaseErrorCode(response.Content);
        throw CreateFirebaseException(firebaseCode, response.Content);
    }

    private static T DeserializeResponse<T>(string content)
    {
        try
        {
            return JsonSerializer.Deserialize<T>(content, JsonOptions)
                ?? throw new JsonException("A resposta estava vazia.");
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException(
                "O Firebase retornou uma resposta em formato inesperado.",
                exception);
        }
    }

    private static string ExtractFirebaseErrorCode(string content)
    {
        try
        {
            using var document = JsonDocument.Parse(content);

            if (document.RootElement.TryGetProperty("error", out var error)
                && error.TryGetProperty("status", out var status))
            {
                var statusCode = status.GetString();

                if (!string.IsNullOrWhiteSpace(statusCode))
                {
                    return statusCode;
                }
            }

            if (document.RootElement.TryGetProperty("error", out error)
                && error.TryGetProperty("message", out var message))
            {
                var rawCode = message.GetString();

                if (!string.IsNullOrWhiteSpace(rawCode))
                {
                    return rawCode.Split(':', 2)[0].Trim();
                }
            }
        }
        catch (JsonException)
        {
        }

        return "UNKNOWN_ERROR";
    }

    private static InvalidOperationException CreateFirebaseException(
        string firebaseCode,
        string responseContent)
    {
        var message = firebaseCode switch
        {
            "EMAIL_EXISTS" => "Já existe uma conta cadastrada com este e-mail.",
            "EMAIL_NOT_FOUND" or "INVALID_PASSWORD" or "INVALID_LOGIN_CREDENTIALS" =>
                "E-mail ou senha inválidos.",
            "USER_DISABLED" => "Esta conta foi desativada.",
            "WEAK_PASSWORD" => "A senha informada é muito fraca.",
            "TOO_MANY_ATTEMPTS_TRY_LATER" =>
                "Muitas tentativas foram realizadas. Tente novamente mais tarde.",
            "OPERATION_NOT_ALLOWED" =>
                "O login por e-mail e senha não está habilitado no Firebase.",
            "PERMISSION_DENIED" => "O Firebase negou acesso ao perfil do usuário.",
            "NOT_FOUND" => "O perfil do usuário não foi encontrado no Firestore.",
            "API_KEY_INVALID" => "A API Key do Firebase é inválida.",
            _ => "Não foi possível concluir a operação no Firebase."
        };

        var exception = new InvalidOperationException(message);
        exception.Data["FirebaseCode"] = firebaseCode;
        exception.Data["FirebaseResponse"] = responseContent;
        return exception;
    }

    private static bool IsInvalidSessionError(string firebaseCode)
    {
        return firebaseCode is
            "INVALID_REFRESH_TOKEN" or
            "TOKEN_EXPIRED" or
            "USER_DISABLED" or
            "USER_NOT_FOUND" or
            "PROJECT_NUMBER_MISMATCH";
    }

    private static string GetRequiredStringField(
        IReadOnlyDictionary<string, FirestoreValueDto> fields,
        string fieldName)
    {
        if (fields.TryGetValue(fieldName, out var field)
            && !string.IsNullOrWhiteSpace(field.StringValue))
        {
            return field.StringValue;
        }

        throw new InvalidOperationException(
            $"O perfil salvo no Firestore não contém o campo obrigatório '{fieldName}'.");
    }

    private static DateTimeOffset GetRequiredTimestampField(
        IReadOnlyDictionary<string, FirestoreValueDto> fields,
        string fieldName)
    {
        if (fields.TryGetValue(fieldName, out var field)
            && field.TimestampValue is not null)
        {
            return field.TimestampValue.Value.ToUniversalTime();
        }

        throw new InvalidOperationException(
            $"O perfil salvo no Firestore não contém o campo obrigatório '{fieldName}'.");
    }

    private string BuildUserDocumentUrl(string userId)
    {
        return $"{FirestoreBaseUrl}/projects/{Escape(_options.ProjectId)}" +
               $"/databases/(default)/documents/users/{Escape(userId)}";
    }

    private void EnsureFirebaseIsConfigured()
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey)
            || string.IsNullOrWhiteSpace(_options.ProjectId))
        {
            throw new InvalidOperationException(
                "Configure FirebaseOptions.ApiKey e FirebaseOptions.ProjectId em MauiProgram.cs.");
        }
    }

    private void ClearLocalSession()
    {
        try
        {
            _secureStorage.Remove(RefreshTokenStorageKey);
        }
        finally
        {
            ClearSessionInMemory();
        }
    }

    private void ClearSessionInMemory()
    {
        _idToken = null;
        _refreshToken = null;
        _idTokenExpiresAtUtc = default;
        CurrentUser = null;
    }

    private static DateTimeOffset GetIdTokenExpiration(string? expiresIn)
    {
        if (!long.TryParse(
                expiresIn,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var expiresInSeconds)
            || expiresInSeconds <= 0)
        {
            throw new InvalidOperationException(
                "O Firebase não retornou a validade esperada para o ID token.");
        }

        return DateTimeOffset.UtcNow.AddSeconds(expiresInSeconds);
    }

    private static void ValidateRequiredValue(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("O valor é obrigatório.", parameterName);
        }
    }

    private static string RequireResponseValue(string? value, string valueName)
    {
        return !string.IsNullOrWhiteSpace(value)
            ? value
            : throw new InvalidOperationException(
                $"O Firebase não retornou o {valueName} esperado.");
    }

    private static string Escape(string value) => Uri.EscapeDataString(value);

    private sealed record FirebaseHttpResponse(bool IsSuccessStatusCode, string Content);

    private sealed record RefreshedTokens(
        string UserId,
        string IdToken,
        string RefreshToken,
        DateTimeOffset ExpiresAtUtc);

    private sealed class FirebaseAuthResponseDto
    {
        [JsonPropertyName("localId")]
        public string? LocalId { get; init; }

        [JsonPropertyName("email")]
        public string? Email { get; init; }

        [JsonPropertyName("idToken")]
        public string? IdToken { get; init; }

        [JsonPropertyName("refreshToken")]
        public string? RefreshToken { get; init; }

        [JsonPropertyName("expiresIn")]
        public string? ExpiresIn { get; init; }
    }

    private sealed class RefreshTokenResponseDto
    {
        [JsonPropertyName("user_id")]
        public string? UserId { get; init; }

        [JsonPropertyName("id_token")]
        public string? IdToken { get; init; }

        [JsonPropertyName("refresh_token")]
        public string? RefreshToken { get; init; }

        [JsonPropertyName("expires_in")]
        public string? ExpiresIn { get; init; }
    }

    private sealed class FirestoreDocumentDto
    {
        [JsonPropertyName("fields")]
        public Dictionary<string, FirestoreValueDto> Fields { get; init; } = [];
    }

    private sealed class FirestoreValueDto
    {
        [JsonPropertyName("stringValue")]
        public string? StringValue { get; init; }

        [JsonPropertyName("timestampValue")]
        public DateTimeOffset? TimestampValue { get; init; }
    }
}
