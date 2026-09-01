using System.Net.Mail;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using UniRota.Services.Interfaces;

namespace UniRota.ViewModels;

public partial class RegisterViewModel : ObservableObject
{
    private readonly IAuthService _authService;

    [ObservableProperty]
    private string name = string.Empty;

    [ObservableProperty]
    private string email = string.Empty;

    [ObservableProperty]
    private string password = string.Empty;

    [ObservableProperty]
    private string confirmPassword = string.Empty;

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private string errorMessage = string.Empty;

    [ObservableProperty]
    private bool hasError;

    public RegisterViewModel(IAuthService authService)
    {
        _authService = authService;
    }

    public bool IsNotBusy => !IsBusy;

    partial void OnIsBusyChanged(bool value)
    {
        OnPropertyChanged(nameof(IsNotBusy));
    }

    [RelayCommand(AllowConcurrentExecutions = false)]
    private async Task RegisterAsync(CancellationToken cancellationToken)
    {
        if (IsBusy || !ValidateFields())
        {
            return;
        }

        IsBusy = true;
        ClearError();

        try
        {
            await _authService.RegisterAsync(Name.Trim(), Email.Trim(), Password, cancellationToken);
            Password = string.Empty;
            ConfirmPassword = string.Empty;
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
    private async Task GoToLoginAsync()
    {
        if (IsBusy)
        {
            return;
        }

        ClearError();
        Password = string.Empty;
        ConfirmPassword = string.Empty;
        await Shell.Current.GoToAsync("//login");
    }

    private bool ValidateFields()
    {
        ClearError();

        if (string.IsNullOrWhiteSpace(Name))
        {
            SetError("Informe seu nome.");
            return false;
        }

        if (string.IsNullOrWhiteSpace(Email) || !MailAddress.TryCreate(Email.Trim(), out _))
        {
            SetError("Informe um e-mail válido.");
            return false;
        }

        if (string.IsNullOrWhiteSpace(Password) || Password.Length < 6)
        {
            SetError("A senha deve ter pelo menos 6 caracteres.");
            return false;
        }

        if (!string.Equals(Password, ConfirmPassword, StringComparison.Ordinal))
        {
            SetError("A confirmação de senha não corresponde à senha.");
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
            ? "Não foi possível criar sua conta. Tente novamente."
            : message;
        HasError = true;
    }
}
