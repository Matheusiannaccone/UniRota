using System.Net.Mail;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using UniRota.Services.Interfaces;

namespace UniRota.ViewModels;

public partial class LoginViewModel : ObservableObject
{
    private readonly IAuthService _authService;

    [ObservableProperty]
    private string email = string.Empty;

    [ObservableProperty]
    private string password = string.Empty;

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private string errorMessage = string.Empty;

    [ObservableProperty]
    private bool hasError;

    public LoginViewModel(IAuthService authService)
    {
        _authService = authService;
    }

    public bool IsNotBusy => !IsBusy;

    partial void OnIsBusyChanged(bool value)
    {
        OnPropertyChanged(nameof(IsNotBusy));
    }

    [RelayCommand(AllowConcurrentExecutions = false)]
    private async Task LoginAsync(CancellationToken cancellationToken)
    {
        if (IsBusy || !ValidateFields())
        {
            return;
        }

        IsBusy = true;
        ClearError();

        try
        {
            await _authService.LoginAsync(Email.Trim(), Password, cancellationToken);
            Password = string.Empty;
            await Shell.Current.GoToAsync("//home");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            SetError(exception.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task GoToRegisterAsync()
    {
        if (IsBusy)
        {
            return;
        }

        ClearError();
        Password = string.Empty;
        await Shell.Current.GoToAsync("//register");
    }

    private bool ValidateFields()
    {
        ClearError();

        if (string.IsNullOrWhiteSpace(Email) || string.IsNullOrWhiteSpace(Password))
        {
            SetError("Informe o e-mail e a senha.");
            return false;
        }

        if (!MailAddress.TryCreate(Email.Trim(), out _))
        {
            SetError("Informe um e-mail válido.");
            return false;
        }

        return true;
    }

    private void ClearError()
    {
        ErrorMessage = string.Empty;
        HasError = false;
    }

    private void SetError(string message)
    {
        ErrorMessage = string.IsNullOrWhiteSpace(message)
            ? "Não foi possível entrar. Tente novamente."
            : message;
        HasError = true;
    }
}
