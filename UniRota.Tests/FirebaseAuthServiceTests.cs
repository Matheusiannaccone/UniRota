using System.Net;
using System.Text.Json;
using Microsoft.Maui.Storage;
using UniRota.Services.Firebase;

namespace UniRota.Tests;

public sealed class FirebaseAuthServiceTests
{
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

    private sealed class AuthFixture : IDisposable
    {
        private readonly HttpClient _client;
        public MemoryStorage Storage { get; } = new();
        public FirebaseAuthService Service { get; }
        public Func<HttpRequestMessage, Task<HttpResponseMessage>> Handle { get; set; } = request =>
            Task.FromResult(Json(request.RequestUri!.AbsolutePath.Contains("signInWithPassword")
                ? """{"localId":"user-1","email":"old@example.com","idToken":"token","refreshToken":"refresh","expiresIn":"3600"}"""
                : """{"fields":{"id":{"stringValue":"user-1"},"name":{"stringValue":"Original"},"email":{"stringValue":"old@example.com"},"createdAtUtc":{"timestampValue":"2026-01-01T00:00:00Z"}}}"""));

        public AuthFixture()
        {
            _client = new HttpClient(new Handler(request => Handle(request)));
            Service = new FirebaseAuthService(_client, Storage,
                new FirebaseOptions { ApiKey = "test-key", ProjectId = "test-project" });
        }

        public void Dispose() => _client.Dispose();
    }

    private sealed class Handler(Func<HttpRequestMessage, Task<HttpResponseMessage>> handle) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => handle(request);
    }

    private sealed class MemoryStorage : ISecureStorage
    {
        public Dictionary<string, string> Values { get; } = [];
        public Task<string?> GetAsync(string key) => Task.FromResult(Values.GetValueOrDefault(key));
        public Task SetAsync(string key, string value) { Values[key] = value; return Task.CompletedTask; }
        public bool Remove(string key) => Values.Remove(key);
        public void RemoveAll() => Values.Clear();
    }
}
