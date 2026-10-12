using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using UniRota.Services.Interfaces;

namespace UniRota.ViewModels;

public partial class ProfileViewModel : ObservableObject
{
    private readonly IAuthService _authService;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSecurityIdle))]
    [NotifyPropertyChangedFor(nameof(IsConfirmingPassword))]
    [NotifyPropertyChangedFor(nameof(IsEnteringEmail))]
    private int securityStep;

    [ObservableProperty] private string currentPassword = string.Empty;
    [ObservableProperty] private string newEmail = string.Empty;
    public bool IsSecurityIdle => SecurityStep == 0;
    public bool IsConfirmingPassword => SecurityStep == 1;
    public bool IsEnteringEmail => SecurityStep == 2;

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

    [RelayCommand(AllowConcurrentExecutions = false)]
    private async Task ConfirmCurrentPasswordAsync()
    {
        if (IsBusy || !IsConfirmingPassword) return;
        IsBusy = true;
        ErrorMessage = SuccessMessage = string.Empty;
        try
        {
            await _authService.ReauthenticateAsync(CurrentPassword);
            SecurityStep = 2;
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
        finally { CurrentPassword = string.Empty; IsBusy = false; }
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
