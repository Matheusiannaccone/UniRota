using System.Net;
using System.Text.Json;
using UniRota.Models;

namespace UniRota.Tests;

public sealed partial class FirebaseAuthServiceTests
{
    private static async Task<AuthFixture> BeginEmailFlowAsync()
    {
        var fixture = new AuthFixture();
        await fixture.Service.LoginAsync("old@example.com", "password");
        await fixture.Service.ReauthenticateAsync("password");
        await fixture.Service.BeginEmailChangeAsync("new@example.com");
        return fixture;
    }

    [Fact]
    public async Task EmailLink_RequiresReauthentication_AndDoesNotUpdateProfileEarly()
    {
        using var f = new AuthFixture();
        var before = await f.Service.LoginAsync("old@example.com", "password");
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Service.BeginEmailChangeAsync("new@example.com"));
        await f.Service.ReauthenticateAsync("password");
        var normal = f.Handle;
        f.Handle = async request =>
        {
            if (request.RequestUri!.AbsolutePath.Contains("sendOobCode"))
            {
                using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync());
                Assert.Equal("VERIFY_AND_CHANGE_EMAIL", body.RootElement.GetProperty("requestType").GetString());
                Assert.Equal("new@example.com", body.RootElement.GetProperty("newEmail").GetString());
                Assert.False(body.RootElement.TryGetProperty("password", out _));
            }
            return await normal(request);
        };
        await f.Service.BeginEmailChangeAsync("new@example.com");
        Assert.Same(before, f.Service.CurrentUser);
        Assert.Equal("old@example.com", f.ProfileEmail);
        Assert.Equal(0, f.Service.PendingEmailChange!.ResendCount);
        Assert.DoesNotContain(f.Requests, r => r.StartsWith("PATCH"));
        Assert.DoesNotContain(f.Storage.Values.Values, value => value.Contains("password"));
    }

    [Theory]
    [InlineData("invalid")]
    [InlineData("Name <a@example.com>")]
    [InlineData("OLD@example.com")]
    public async Task EmailLink_RejectsInvalidOrUnchangedEmail(string email)
    {
        using var f = new AuthFixture();
        await f.Service.LoginAsync("old@example.com", "password");
        await f.Service.ReauthenticateAsync("password");
        await Assert.ThrowsAsync<ArgumentException>(() => f.Service.BeginEmailChangeAsync(email));
        Assert.Null(f.Service.PendingEmailChange);
        Assert.DoesNotContain(f.Requests, r => r.Contains("sendOobCode"));
    }

    [Fact]
    public async Task EmailFlow_CooldownAndTwoResends_AreEnforcedInService()
    {
        using var f = await BeginEmailFlowAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Service.ResendEmailConfirmationAsync());
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Service.CheckEmailConfirmationAsync(EmailConfirmationAction.Manual));
        for (var i = 0; i < 2; i++)
        {
            f.Clock.Advance();
            await f.Service.ResendEmailConfirmationAsync();
            Assert.Equal(f.Clock.GetUtcNow().AddSeconds(60), f.Service.PendingEmailChange!.ActionsAvailableAtUtc);
        }
        f.Clock.Advance();
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Service.ResendEmailConfirmationAsync());
        Assert.Equal(3, f.Requests.Count(r => r.Contains("sendOobCode")));
        Assert.Equal(EmailConfirmationResult.Waiting, await f.Service.CheckEmailConfirmationAsync(EmailConfirmationAction.Manual));
        Assert.Equal(2, f.Service.PendingEmailChange!.ResendCount);
        Assert.Equal(f.Clock.GetUtcNow().AddSeconds(60), f.Service.PendingEmailChange.ActionsAvailableAtUtc);
    }

    [Fact]
    public async Task AutomaticCheckAndCancel_WorkDuringCooldown_WithoutEarlyWrites()
    {
        using var f = await BeginEmailFlowAsync();
        var before = f.Service.CurrentUser;
        Assert.Equal(EmailConfirmationResult.Waiting, await f.Service.CheckEmailConfirmationAsync(EmailConfirmationAction.Automatic));
        Assert.Equal(EmailConfirmationResult.Cancelled, await f.Service.CheckEmailConfirmationAsync(EmailConfirmationAction.Cancel));
        Assert.Same(before, f.Service.CurrentUser);
        Assert.Null(f.Service.PendingEmailChange);
        Assert.Single(f.Storage.Values);
        Assert.DoesNotContain(f.Requests, r => r.StartsWith("PATCH"));
    }

    [Theory]
    [InlineData(EmailConfirmationAction.Automatic)]
    [InlineData(EmailConfirmationAction.Cancel)]
    public async Task ConfirmedEmail_SynchronizesEvenWhenCancelling(EmailConfirmationAction action)
    {
        using var f = await BeginEmailFlowAsync();
        var before = f.Service.CurrentUser!;
        f.AccountEmail = "new@example.com";
        Assert.Equal(EmailConfirmationResult.Confirmed, await f.Service.CheckEmailConfirmationAsync(action));
        Assert.Equal("new@example.com", f.ProfileEmail);
        Assert.Equal(f.ProfileEmail, f.Service.CurrentUser!.Email);
        Assert.Equal(before.Name, f.Service.CurrentUser.Name);
        Assert.Equal(before.Id, f.Service.CurrentUser.Id);
        Assert.Equal(before.CreatedAtUtc, f.Service.CurrentUser.CreatedAtUtc);
        Assert.Null(f.Service.PendingEmailChange);
        Assert.Single(f.Storage.Values);
    }

    [Fact]
    public async Task SynchronizationFailure_RemainsExplicitAndRecoverable()
    {
        using var f = await BeginEmailFlowAsync();
        f.AccountEmail = "new@example.com";
        var normal = f.Handle;
        f.Handle = request => request.Method == HttpMethod.Patch
            ? Task.FromResult(Json("""{"error":{"status":"PERMISSION_DENIED"}}""", HttpStatusCode.Forbidden)) : normal(request);
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => f.Service.CheckEmailConfirmationAsync(EmailConfirmationAction.Cancel));
        Assert.Contains("já foi alterado", error.Message);
        Assert.Equal("old@example.com", f.Service.CurrentUser!.Email);
        Assert.NotNull(f.Service.PendingEmailChange);
        f.Handle = normal;
        Assert.Equal(EmailConfirmationResult.Confirmed, await f.Service.CheckEmailConfirmationAsync(EmailConfirmationAction.Automatic));
    }

    [Fact]
    public async Task Restart_PreservesDestinationCounterAndCooldown()
    {
        using var f = await BeginEmailFlowAsync();
        f.Clock.Advance();
        await f.Service.ResendEmailConfirmationAsync();
        using var restarted = new AuthFixture(f.Storage, f.Clock);
        await restarted.Service.RestoreSessionAsync();
        Assert.Equal(f.Service.PendingEmailChange, restarted.Service.PendingEmailChange);
        await Assert.ThrowsAsync<InvalidOperationException>(() => restarted.Service.ResendEmailConfirmationAsync());
    }

    [Fact]
    public async Task InvalidSession_KeepsPendingFlow_AndRecoveryDoesNotUpdateEmailEarly()
    {
        using var f = await BeginEmailFlowAsync();
        using var restarted = new AuthFixture(f.Storage, f.Clock);
        var normal = restarted.Handle;
        restarted.Handle = request => request.RequestUri!.AbsolutePath.EndsWith("/token")
            ? Task.FromResult(Json("""{"error":{"message":"TOKEN_EXPIRED"}}""", HttpStatusCode.BadRequest)) : normal(request);
        Assert.Null(await restarted.Service.RestoreSessionAsync());
        Assert.NotNull(restarted.Service.PendingEmailChange);
        restarted.Handle = normal;
        restarted.AccountEmail = "new@example.com";
        await restarted.Service.RecoverEmailChangeSessionAsync("password");
        Assert.Equal("old@example.com", restarted.Service.CurrentUser!.Email);
        Assert.Equal(EmailConfirmationResult.Confirmed, await restarted.Service.CheckEmailConfirmationAsync(EmailConfirmationAction.Automatic));
        Assert.Equal("new@example.com", restarted.Service.CurrentUser!.Email);
        Assert.Single(restarted.Storage.Values);
    }

    [Fact]
    public async Task CancelLookupFailure_DoesNotDiscardPendingState()
    {
        using var f = await BeginEmailFlowAsync();
        var pending = f.Service.PendingEmailChange;
        f.Handle = _ => throw new HttpRequestException("offline");
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Service.CheckEmailConfirmationAsync(EmailConfirmationAction.Cancel));
        Assert.Equal(pending, f.Service.PendingEmailChange);
        Assert.Equal("old@example.com", f.Service.CurrentUser!.Email);
    }

    [Fact]
    public async Task ConcurrentInitialSubmits_OnlySendOneLink()
    {
        using var f = new AuthFixture();
        await f.Service.LoginAsync("old@example.com", "password");
        await f.Service.ReauthenticateAsync("password");
        var sent = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var normal = f.Handle;
        f.Handle = async request => { sent.TrySetResult(); await release.Task; return await normal(request); };
        var first = f.Service.BeginEmailChangeAsync("new@example.com");
        await sent.Task;
        var second = f.Service.BeginEmailChangeAsync("another@example.com");
        release.SetResult();
        await first;
        await Assert.ThrowsAsync<InvalidOperationException>(() => second);
        Assert.Single(f.Requests.Where(r => r.Contains("sendOobCode")));
    }

    [Fact]
    public async Task Login_ReconcilesPreviouslyConfirmedEmail()
    {
        using var f = new AuthFixture { AccountEmail = "new@example.com" };
        var user = await f.Service.LoginAsync("new@example.com", "password");
        Assert.Equal("new@example.com", user.Email);
        Assert.Equal(user.Email, f.ProfileEmail);
    }
}
