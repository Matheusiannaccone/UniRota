using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using UniRota.Services.Interfaces;

namespace UniRota.ViewModels;

public partial class ProfileViewModel : ObservableObject
{
    private readonly IAuthService _authService;
    private bool _changingPassword;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSecurityIdle))]
    [NotifyPropertyChangedFor(nameof(IsConfirmingPassword))]
    [NotifyPropertyChangedFor(nameof(IsEnteringEmail))]
    [NotifyPropertyChangedFor(nameof(IsEnteringPassword))]
    private int securityStep;

    [ObservableProperty] private string currentPassword = string.Empty;
    [ObservableProperty] private string newEmail = string.Empty;
    [ObservableProperty] private string newPassword = string.Empty;
    [ObservableProperty] private string confirmNewPassword = string.Empty;
    public bool IsSecurityIdle => SecurityStep == 0;
    public bool IsConfirmingPassword => SecurityStep == 1;
    public bool IsEnteringEmail => SecurityStep == 2;
    public bool IsEnteringPassword => SecurityStep == 3;

    [ObservableProperty]
    private string name = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotBusy))]
    private bool isBusy;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string errorMessage = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSuccess))]
    private string successMessage = string.Empty;

    public bool HasSuccess => !string.IsNullOrEmpty(SuccessMessage);

    public bool IsNotBusy => !IsBusy;
    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    public ProfileViewModel(IAuthService authService) => _authService = authService;

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (_authService.PendingEmailChange is not null)
        {
            await Shell.Current.GoToAsync(AppShell.EmailConfirmationRoute);
            return;
        }
        if (_authService.CurrentUser is not { } user)
        {
            await Shell.Current.GoToAsync("//login");
            return;
        }
        Name = user.Name;
        ErrorMessage = string.Empty;
        SuccessMessage = string.Empty;
    }

    public void ClearSensitiveFields()
    {
        CurrentPassword = NewEmail = string.Empty;
        NewPassword = ConfirmNewPassword = string.Empty;
        _changingPassword = false;
        SecurityStep = 0;
        _authService.ClearReauthentication();
    }

    [RelayCommand]
    private void BeginEmailChange()
    {
        if (IsBusy) return;
        ClearSensitiveFields();
        ErrorMessage = SuccessMessage = string.Empty;
        SecurityStep = 1;
    }

    [RelayCommand]
    private void CancelSecurityChange()
    {
        if (!IsBusy) ClearSensitiveFields();
    }

    [RelayCommand]
    private void BeginPasswordChange()
    {
        if (IsBusy) return;
        ClearSensitiveFields();
        ErrorMessage = SuccessMessage = string.Empty;
        _changingPassword = true;
        SecurityStep = 1;
    }

    [RelayCommand(AllowConcurrentExecutions = false)]
    private async Task ConfirmCurrentPasswordAsync()
    {
        if (IsBusy || !IsConfirmingPassword) return;
        IsBusy = true;
        ErrorMessage = SuccessMessage = string.Empty;
        try
        {
            await _authService.ReauthenticateAsync(CurrentPassword);
            SecurityStep = _changingPassword ? 3 : 2;
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
        finally { CurrentPassword = string.Empty; IsBusy = false; }
    }

    [RelayCommand(AllowConcurrentExecutions = false)]
    private async Task SubmitPasswordAsync()
    {
        if (IsBusy || !IsEnteringPassword) return;
        ErrorMessage = SuccessMessage = string.Empty;
        if (string.IsNullOrWhiteSpace(NewPassword) || NewPassword.Length < 6)
        {
            ErrorMessage = "A senha deve ter pelo menos 6 caracteres.";
            return;
        }
        if (!string.Equals(NewPassword, ConfirmNewPassword, StringComparison.Ordinal))
        {
            ErrorMessage = "A confirmação de senha não corresponde à nova senha.";
            return;
        }
        IsBusy = true;
        try
        {
            await _authService.ChangePasswordAsync(NewPassword);
            ClearSensitiveFields();
            SuccessMessage = "Senha atualizada.";
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            SecurityStep = 1;
            if (_authService.CurrentUser is null)
            {
                await Shell.Current.DisplayAlert("Sessão", ErrorMessage, "OK");
                await Shell.Current.GoToAsync("//login");
            }
        }
        finally
        {
            CurrentPassword = NewPassword = ConfirmNewPassword = string.Empty;
            IsBusy = false;
        }
    }

    [RelayCommand(AllowConcurrentExecutions = false)]
    private async Task SubmitEmailAsync()
    {
        if (IsBusy || !IsEnteringEmail) return;
        IsBusy = true;
        ErrorMessage = SuccessMessage = string.Empty;
        try { await _authService.BeginEmailChangeAsync(NewEmail); }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            if (ex.Data["FirebaseCode"] is "CREDENTIAL_TOO_OLD_LOGIN_AGAIN" or "REQUIRES_RECENT_LOGIN" or "INVALID_ID_TOKEN" or "TOKEN_EXPIRED")
                SecurityStep = 1;
        }
        finally { IsBusy = false; }
        if (_authService.PendingEmailChange is not null)
        {
            ClearSensitiveFields();
            try { await Shell.Current.GoToAsync(AppShell.EmailConfirmationRoute); }
            catch (Exception) { ErrorMessage = "O fluxo de confirmação foi salvo. Abra o Perfil novamente para retomá-lo."; }
        }
    }

    [RelayCommand(AllowConcurrentExecutions = false)]
    private async Task SaveNameAsync()
    {
        if (IsBusy) return;
        ErrorMessage = SuccessMessage = string.Empty;
        if (string.IsNullOrWhiteSpace(Name))
        {
            ErrorMessage = "Informe seu nome.";
            return;
        }
        IsBusy = true;
        try
        {
            await _authService.UpdateNameAsync(Name);
            Name = _authService.CurrentUser?.Name ?? Name.Trim();
            SuccessMessage = "Nome atualizado.";
        }
        catch (Exception exception) { ErrorMessage = exception.Message; }
        finally { IsBusy = false; }
    }

    [RelayCommand(AllowConcurrentExecutions = false)]
    private async Task LogoutAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        ErrorMessage = string.Empty;
        try
        {
            await _authService.LogoutAsync();
            await Shell.Current.GoToAsync("//login");
        }
        catch (Exception)
        {
            ErrorMessage = "Não foi possível sair. Tente novamente.";
        }
        finally { IsBusy = false; }
    }
}
