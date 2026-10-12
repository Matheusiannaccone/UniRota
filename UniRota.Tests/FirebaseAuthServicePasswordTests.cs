using System.Net;
using System.Text.Json;
using UniRota.ViewModels;

namespace UniRota.Tests;

public sealed partial class FirebaseAuthServiceTests
{
    [Fact]
    public async Task PasswordScreen_RequiresCurrentPassword_AndClearsItAfterEachAttempt()
    {
        using var f = new AuthFixture();
        await f.Service.LoginAsync("old@example.com", "password");
        var vm = new ProfileViewModel(f.Service);
        vm.BeginPasswordChangeCommand.Execute(null);
        Assert.True(vm.IsConfirmingPassword);
        Assert.False(vm.IsEnteringPassword);
        var normal = f.Handle;
        f.Handle = _ => Task.FromResult(Json("""{"error":{"message":"INVALID_PASSWORD"}}""", HttpStatusCode.BadRequest));
        vm.CurrentPassword = "wrong-password";
        await vm.ConfirmCurrentPasswordCommand.ExecuteAsync(null);
        Assert.True(vm.IsConfirmingPassword);
        Assert.True(vm.HasError);
        Assert.Empty(vm.CurrentPassword);
        f.Handle = normal;
        vm.CurrentPassword = "password";
        await vm.ConfirmCurrentPasswordCommand.ExecuteAsync(null);
        Assert.True(vm.IsEnteringPassword);
        Assert.Empty(vm.CurrentPassword);
    }

    [Fact]
    public async Task PasswordScreen_ValidatesConfirmation_AndClearsFieldsOnSuccess()
    {
        using var f = new AuthFixture();
        await f.Service.LoginAsync("old@example.com", "password");
        var vm = new ProfileViewModel(f.Service);
        vm.BeginPasswordChangeCommand.Execute(null);
        vm.CurrentPassword = "password";
        await vm.ConfirmCurrentPasswordCommand.ExecuteAsync(null);
        vm.NewPassword = "new-password";
        vm.ConfirmNewPassword = "different-password";
        await vm.SubmitPasswordCommand.ExecuteAsync(null);
        Assert.Contains("não corresponde", vm.ErrorMessage);
        Assert.DoesNotContain(f.Requests, r => r.Contains("accounts:update"));
        vm.ConfirmNewPassword = vm.NewPassword;
        f.Handle = _ => Task.FromResult(Json("""{"localId":"user-1","idToken":"new-token","refreshToken":"new-refresh","expiresIn":"3600"}"""));
        await vm.SubmitPasswordCommand.ExecuteAsync(null);
        Assert.True(vm.IsSecurityIdle);
        Assert.True(vm.HasSuccess);
        Assert.Empty(vm.CurrentPassword);
        Assert.Empty(vm.NewPassword);
        Assert.Empty(vm.ConfirmNewPassword);
        Assert.False(vm.IsBusy);
    }

    [Fact]
    public async Task PasswordChange_RequiresReauthentication()
    {
        using var f = new AuthFixture();
        await f.Service.LoginAsync("old@example.com", "password");
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Service.ChangePasswordAsync("new-password"));
        Assert.DoesNotContain(f.Requests, r => r.Contains("accounts:update"));
    }

    [Fact]
    public async Task PasswordChange_UpdatesOnlyAuth_RotatesTokens_AndConsumesReauthentication()
    {
        using var f = new AuthFixture();
        var before = await f.Service.LoginAsync("old@example.com", "password");
        await f.Service.ReauthenticateAsync("password");
        var calls = 0;
        f.Handle = async request =>
        {
            calls++;
            Assert.EndsWith("accounts:update", request.RequestUri!.AbsolutePath);
            using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync());
            Assert.Equal("new-password", body.RootElement.GetProperty("password").GetString());
            Assert.Equal("token", body.RootElement.GetProperty("idToken").GetString());
            Assert.True(body.RootElement.GetProperty("returnSecureToken").GetBoolean());
            return Json("""{"localId":"user-1","idToken":"new-token","refreshToken":"new-refresh","expiresIn":"3600","passwordHash":"must-not-be-stored"}""");
        };
        await f.Service.ChangePasswordAsync("new-password");
        Assert.Same(before, f.Service.CurrentUser);
        Assert.Equal("new-token", await f.Service.GetValidIdTokenAsync());
        Assert.Equal("new-refresh", Assert.Single(f.Storage.Values).Value);
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Service.ChangePasswordAsync("another-password"));
        Assert.Equal(1, calls);
    }

    [Theory]
    [InlineData("")]
    [InlineData("      ")]
    [InlineData("12345")]
    public async Task PasswordChange_RejectsEmptyOrShortPassword(string password)
    {
        using var f = new AuthFixture();
        await Assert.ThrowsAsync<ArgumentException>(() => f.Service.ChangePasswordAsync(password));
        Assert.Empty(f.Requests);
    }

    [Theory]
    [InlineData("WEAK_PASSWORD", "fraca")]
    [InlineData("CREDENTIAL_TOO_OLD_LOGIN_AGAIN", "Confirme")]
    [InlineData("INVALID_ID_TOKEN", "sessão")]
    public async Task PasswordChange_MapsFirebaseErrorsAndPreservesProfile(string code, string message)
    {
        using var f = new AuthFixture();
        var before = await f.Service.LoginAsync("old@example.com", "password");
        await f.Service.ReauthenticateAsync("password");
        f.Handle = _ => Task.FromResult(Json(JsonSerializer.Serialize(new { error = new { status = "INVALID_ARGUMENT", message = code } }), HttpStatusCode.BadRequest));
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => f.Service.ChangePasswordAsync("new-password"));
        Assert.Contains(message, ex.Message);
        Assert.Same(before, f.Service.CurrentUser);
        Assert.Equal("refresh", Assert.Single(f.Storage.Values).Value);
    }

    [Fact]
    public async Task PasswordChange_WhenTokenPersistenceFails_ReportsPasswordChangedAndClearsSession()
    {
        using var f = new AuthFixture();
        await f.Service.LoginAsync("old@example.com", "password");
        await f.Service.ReauthenticateAsync("password");
        f.Handle = _ => Task.FromResult(Json("""{"localId":"user-1","idToken":"new-token","refreshToken":"new-refresh","expiresIn":"3600"}"""));
        f.Storage.FailWrites = true;
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => f.Service.ChangePasswordAsync("new-password"));
        Assert.Contains("A senha foi alterada", ex.Message);
        Assert.Null(f.Service.CurrentUser);
        Assert.Empty(f.Storage.Values);
    }
}
