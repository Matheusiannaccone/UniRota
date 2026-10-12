using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using UniRota.Models;
using UniRota.Services.Interfaces;

namespace UniRota.ViewModels;

public partial class AwaitingEmailConfirmationViewModel : ObservableObject
{
    private readonly IAuthService _authService;
    private EmailConfirmationAction _recoveryAction = EmailConfirmationAction.Automatic;
    private EmailConfirmationResult _completion;
    private bool _automaticCheckRequested;

    [ObservableProperty] private string destinationEmail = string.Empty;
    [ObservableProperty] private string statusMessage = string.Empty;
    [ObservableProperty] private string cooldownMessage = string.Empty;
    [ObservableProperty] private string recoveryPassword = string.Empty;
    [ObservableProperty] private bool needsRecovery;
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private bool canResend;
    [ObservableProperty] private bool canVerify;
    [ObservableProperty] private bool resendLimitReached;
    public bool IsNotBusy => !IsBusy;

    public AwaitingEmailConfirmationViewModel(IAuthService authService) => _authService = authService;

    partial void OnIsBusyChanged(bool value)
    {
        OnPropertyChanged(nameof(IsNotBusy));
        RefreshState();
    }

    public void RefreshState()
    {
        var pending = _authService.PendingEmailChange;
        DestinationEmail = pending?.NewEmail ?? string.Empty;
        var seconds = pending is null ? 0 : Math.Max(0, (int)Math.Ceiling((pending.ActionsAvailableAtUtc - DateTimeOffset.UtcNow).TotalSeconds));
        CooldownMessage = seconds > 0 ? $"As ações ficam disponíveis em {seconds}s." : "Verificação manual disponível.";
        ResendLimitReached = pending?.ResendCount >= 2;
        CanVerify = pending is not null && !IsBusy && !NeedsRecovery && seconds == 0;
        CanResend = CanVerify && !ResendLimitReached;
    }

    public async Task CheckAutomaticallyAsync()
    {
        if (IsBusy) { _automaticCheckRequested = true; return; }
        RefreshState();
        if (_authService.PendingEmailChange is null)
        {
            await FinishAsync(_completion, false);
            return;
        }
        if (!NeedsRecovery) await CheckAsync(EmailConfirmationAction.Automatic);
    }

    [RelayCommand(AllowConcurrentExecutions = false)]
    private async Task VerifyAsync()
    {
        RefreshState();
        if (CanVerify) await CheckAsync(EmailConfirmationAction.Manual);
    }

    [RelayCommand(AllowConcurrentExecutions = false)]
    private async Task CancelAsync()
    {
        if (IsBusy) return;
        _recoveryAction = EmailConfirmationAction.Cancel;
        await CheckAsync(EmailConfirmationAction.Cancel);
    }

    private async Task CheckAsync(EmailConfirmationAction action)
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            var result = await _authService.CheckEmailConfirmationAsync(action);
            if (result == EmailConfirmationResult.Waiting)
                StatusMessage = "A confirmação ainda não foi identificada. Abra o link recebido e retorne ao UniRota.";
            else
            {
                _completion = result;
                RecoveryPassword = string.Empty;
                NeedsRecovery = false;
                await FinishAsync(result, action == EmailConfirmationAction.Cancel);
            }
        }
        catch (Exception ex) { HandleError(ex, action); }
        finally { await EndOperationAsync(); }
    }

    [RelayCommand(AllowConcurrentExecutions = false)]
    private async Task ResendAsync()
    {
        RefreshState();
        if (!CanResend) return;
        IsBusy = true;
        try
        {
            await _authService.ResendEmailConfirmationAsync();
            StatusMessage = "Novo link solicitado. Confira também a pasta de spam.";
        }
        catch (Exception ex) { HandleError(ex, EmailConfirmationAction.Automatic); }
        finally { await EndOperationAsync(); }
    }

    [RelayCommand(AllowConcurrentExecutions = false)]
    private async Task RecoverSessionAsync()
    {
        if (IsBusy || !NeedsRecovery) return;
        IsBusy = true;
        var recovered = false;
        try
        {
            await _authService.RecoverEmailChangeSessionAsync(RecoveryPassword);
            NeedsRecovery = false;
            recovered = true;
        }
        catch (Exception ex) { StatusMessage = ex.Message; }
        finally { RecoveryPassword = string.Empty; IsBusy = false; }
        if (recovered) await CheckAsync(_recoveryAction);
    }

    private async Task EndOperationAsync()
    {
        IsBusy = false;
        if (!_automaticCheckRequested) return;
        _automaticCheckRequested = false;
        if (_authService.PendingEmailChange is not null && !NeedsRecovery)
            await CheckAutomaticallyAsync();
    }

    private void HandleError(Exception ex, EmailConfirmationAction action)
    {
        StatusMessage = ex.Message;
        if (_authService.CurrentUser is null || ex.Data["FirebaseCode"] is
            "TOKEN_EXPIRED" or "INVALID_ID_TOKEN" or "INVALID_REFRESH_TOKEN" or "CREDENTIAL_TOO_OLD_LOGIN_AGAIN" or "REQUIRES_RECENT_LOGIN")
        {
            _recoveryAction = action == EmailConfirmationAction.Manual ? EmailConfirmationAction.Automatic : action;
            NeedsRecovery = true;
            StatusMessage = "Sua sessão precisa ser renovada. Confirme sua senha para consultar a conta e concluir ou cancelar com segurança.";
        }
    }

    private async Task FinishAsync(EmailConfirmationResult result, bool cancelling)
    {
        await Shell.Current.GoToAsync(AppShell.ProfileRoute);
        if (result == EmailConfirmationResult.Confirmed)
            await Shell.Current.DisplayAlert("Perfil", cancelling
                ? "O novo e-mail já havia sido confirmado. Seu perfil foi sincronizado."
                : "E-mail confirmado e perfil atualizado.", "OK");
        else if (result == EmailConfirmationResult.Cancelled)
            await Shell.Current.DisplayAlert("Perfil", "Acompanhamento cancelado. O link já enviado pode continuar válido; se utilizado, o e-mail será reconciliado no próximo acesso.", "OK");
        StatusMessage = string.Empty;
        RefreshState();
    }
}
