using System.Net;
using System.Text.Json;
using Microsoft.Maui.Storage;
using UniRota.Services.Firebase;

namespace UniRota.Tests;

public sealed partial class FirebaseAuthServiceTests
{
    [Fact]
    public async Task Reauthenticate_UsesCurrentEmail_RenewsTokens_WithoutPersistingPassword()
    {
        using var fixture = new AuthFixture();
        var before = await fixture.Service.LoginAsync("old@example.com", "password");
        fixture.Handle = async request =>
        {
            Assert.EndsWith("accounts:signInWithPassword", request.RequestUri!.AbsolutePath);
            using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync());
            Assert.Equal(before.Email, body.RootElement.GetProperty("email").GetString());
            Assert.Equal("current-password", body.RootElement.GetProperty("password").GetString());
            return Json("""{"localId":"user-1","email":"old@example.com","idToken":"reauth-token","refreshToken":"reauth-refresh","expiresIn":"3600"}""");
        };
        await fixture.Service.ReauthenticateAsync("current-password");
        Assert.Same(before, fixture.Service.CurrentUser);
        Assert.Equal("reauth-token", await fixture.Service.GetValidIdTokenAsync());
        Assert.Equal("reauth-refresh", Assert.Single(fixture.Storage.Values).Value);
    }

    [Theory]
    [InlineData("INVALID_PASSWORD")]
    [InlineData("INVALID_LOGIN_CREDENTIALS")]
    public async Task Reauthenticate_IncorrectPassword_PreservesSession(string error)
    {
        using var fixture = new AuthFixture();
        var before = await fixture.Service.LoginAsync("old@example.com", "password");
        fixture.Handle = _ => Task.FromResult(Json(
            JsonSerializer.Serialize(new { error = new { message = error } }), HttpStatusCode.BadRequest));
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Service.ReauthenticateAsync("wrong-password"));
        Assert.Equal("Senha inválida.", exception.Message);
        Assert.Same(before, fixture.Service.CurrentUser);
        Assert.Equal("token", await fixture.Service.GetValidIdTokenAsync());
    }

    [Theory]
    [InlineData("INVALID_PASSWORD")]
    [InlineData("INVALID_LOGIN_CREDENTIALS")]
    public async Task Login_IncorrectCredentials_KeepsGlobalMessage(string error)
    {
        using var fixture = new AuthFixture();
        fixture.Handle = _ => Task.FromResult(Json(
            JsonSerializer.Serialize(new { error = new { message = error } }), HttpStatusCode.BadRequest));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => fixture.Service.LoginAsync("old@example.com", "wrong-password"));

        Assert.Equal("E-mail ou senha inválidos.", exception.Message);
    }

    [Fact]
    public async Task Reauthenticate_DifferentUid_DoesNotReplaceSession()
    {
        using var fixture = new AuthFixture();
        var before = await fixture.Service.LoginAsync("old@example.com", "password");
        fixture.Handle = _ => Task.FromResult(Json("""{"localId":"other-user","idToken":"other-token","refreshToken":"other-refresh","expiresIn":"3600"}"""));
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Service.ReauthenticateAsync("password"));
        Assert.Same(before, fixture.Service.CurrentUser);
        Assert.Equal("refresh", Assert.Single(fixture.Storage.Values).Value);
    }

    [Fact]
    public async Task UpdateName_PatchesOnlyName_AndPreservesIdentity()
    {
        using var fixture = new AuthFixture();
        var before = await fixture.Service.LoginAsync("old@example.com", "password");
        fixture.Handle = async request =>
        {
            Assert.Equal(HttpMethod.Patch, request.Method);
            Assert.Contains("updateMask.fieldPaths=name", request.RequestUri!.Query);
            Assert.Contains("currentDocument.exists=true", request.RequestUri.Query);
            Assert.Equal("token", request.Headers.Authorization!.Parameter);
            using var json = JsonDocument.Parse(await request.Content!.ReadAsStringAsync());
            var field = Assert.Single(json.RootElement.GetProperty("fields").EnumerateObject());
            Assert.Equal("name", field.Name);
            Assert.Equal("Novo nome", field.Value.GetProperty("stringValue").GetString());
            return Json("{}");
        };

        await fixture.Service.UpdateNameAsync("  Novo nome  ");

        var after = fixture.Service.CurrentUser!;
        Assert.Equal("Novo nome", after.Name);
        Assert.Equal(before.Id, after.Id);
        Assert.Equal(before.Email, after.Email);
        Assert.Equal(before.CreatedAtUtc, after.CreatedAtUtc);
    }

    [Fact]
    public async Task UpdateName_Failure_DoesNotChangeCurrentUser()
    {
        using var fixture = new AuthFixture();
        var before = await fixture.Service.LoginAsync("old@example.com", "password");
        fixture.Handle = _ => Task.FromResult(Json("""{"error":{"status":"PERMISSION_DENIED"}}""", HttpStatusCode.Forbidden));
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Service.UpdateNameAsync("Novo"));
        Assert.Same(before, fixture.Service.CurrentUser);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task UpdateName_Empty_DoesNotCallFirebase(string name)
    {
        using var fixture = new AuthFixture();
        fixture.Handle = _ => throw new Xunit.Sdk.XunitException("Unexpected HTTP request");
        await Assert.ThrowsAsync<ArgumentException>(() => fixture.Service.UpdateNameAsync(name));
    }

    [Fact]
    public async Task UpdateName_WithoutSession_IsRejected()
    {
        using var fixture = new AuthFixture();
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Service.UpdateNameAsync("Novo"));
    }

    [Fact]
    public async Task UpdateName_LogoutDuringRequest_DoesNotRestoreSession()
    {
        using var fixture = new AuthFixture();
        await fixture.Service.LoginAsync("old@example.com", "password");
        fixture.Handle = async _ =>
        {
            await fixture.Service.LogoutAsync();
            return Json("{}");
        };
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Service.UpdateNameAsync("Novo"));
        Assert.Null(fixture.Service.CurrentUser);
    }

    private static HttpResponseMessage Json(string body, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(body) };

    private sealed class TestClock : TimeProvider
    {
        private DateTimeOffset _now = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(int seconds = 61) => _now = _now.AddSeconds(seconds);
    }

    private sealed class AuthFixture : IDisposable
    {
        private readonly HttpClient _client;
        public MemoryStorage Storage { get; }
        public TestClock Clock { get; }
        public FirebaseAuthService Service { get; }
        public string AccountEmail { get; set; } = "old@example.com";
        public string ProfileEmail { get; set; } = "old@example.com";
        public List<string> Requests { get; } = [];
        public Func<HttpRequestMessage, Task<HttpResponseMessage>> Handle { get; set; }

        public AuthFixture(MemoryStorage? storage = null, TestClock? clock = null)
        {
            Storage = storage ?? new MemoryStorage();
            Clock = clock ?? new TestClock();
            Handle = RespondAsync;
            _client = new HttpClient(new Handler(request => Handle(request)));
            Service = new FirebaseAuthService(_client, Storage,
                new FirebaseOptions { ApiKey = "test-key", ProjectId = "test-project" }, Clock);
        }

        private async Task<HttpResponseMessage> RespondAsync(HttpRequestMessage request)
        {
            var path = request.RequestUri!.AbsolutePath;
            Requests.Add(request.Method + " " + path);
            var account = new { localId = "user-1", email = AccountEmail, idToken = "token", refreshToken = "refresh", expiresIn = "3600" };
            if (path.Contains("signInWithPassword")) return Json(JsonSerializer.Serialize(account));
            if (path.Contains("accounts:lookup")) return Json(JsonSerializer.Serialize(new { users = new[] { account } }));
            if (path.EndsWith("/token")) return Json("""{"user_id":"user-1","id_token":"token","refresh_token":"refresh","expires_in":"3600"}""");
            if (path.Contains("sendOobCode")) return Json("{}");
            if (request.Method == HttpMethod.Patch)
            {
                using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync());
                if (body.RootElement.GetProperty("fields").TryGetProperty("email", out var email)) ProfileEmail = email.GetProperty("stringValue").GetString()!;
                return Json("{}");
            }
            return Json(JsonSerializer.Serialize(new { fields = new {
                id = new { stringValue = "user-1" }, name = new { stringValue = "Original" },
                email = new { stringValue = ProfileEmail }, createdAtUtc = new { timestampValue = "2026-01-01T00:00:00Z" }
            } }));
        }

        public void Dispose() => _client.Dispose();
    }
    private sealed class Handler(Func<HttpRequestMessage, Task<HttpResponseMessage>> handle) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => handle(request);
    }

    private sealed class MemoryStorage : ISecureStorage
    {
        public bool FailWrites { get; set; }
        public Dictionary<string, string> Values { get; } = [];
        public Task<string?> GetAsync(string key) => Task.FromResult(Values.GetValueOrDefault(key));
        public Task SetAsync(string key, string value)
        {
            if (FailWrites) throw new InvalidOperationException("Storage unavailable");
            Values[key] = value;
            return Task.CompletedTask;
        }
        public bool Remove(string key) => Values.Remove(key);
        public void RemoveAll() => Values.Clear();
    }
}
