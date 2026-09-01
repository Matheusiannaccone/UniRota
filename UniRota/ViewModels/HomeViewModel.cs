using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using UniRota.Services.Interfaces;
using UniRota.Views.Routes;

namespace UniRota.ViewModels;

public partial class HomeViewModel : ObservableObject
{
    private readonly IAuthService _authService;

    [ObservableProperty]
    private string welcomeMessage = "Bem-vindo ao UniRota";

    [ObservableProperty]
    private string email = string.Empty;

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private string errorMessage = string.Empty;

    [ObservableProperty]
    private bool hasError;

    public HomeViewModel(IAuthService authService)
    {
        _authService = authService;
    }

    public bool IsNotBusy => !IsBusy;

    partial void OnIsBusyChanged(bool value)
    {
        OnPropertyChanged(nameof(IsNotBusy));
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        ClearError();

        var user = _authService.CurrentUser;
        if (user is null)
        {
            await Shell.Current.GoToAsync("//login");
            return;
        }

        WelcomeMessage = $"Olá, {user.Name}!";
        Email = user.Email;
    }

    [RelayCommand(AllowConcurrentExecutions = false)]
    private async Task GoToMyRoutesAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        ClearError();

        try
        {
            await Shell.Current.GoToAsync(nameof(MyRoutesPage));
        }
        catch (Exception exception)
        {
            SetError(
                exception.Message,
                "Não foi possível abrir suas rotas. Tente novamente.");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand(AllowConcurrentExecutions = false)]
    private async Task LogoutAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        ClearError();

        try
        {
            await _authService.LogoutAsync();
            await Shell.Current.GoToAsync("//login");
        }
        catch (Exception exception)
        {
            SetError(exception.Message, "Não foi possível sair. Tente novamente.");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ClearError()
    {
        ErrorMessage = string.Empty;
        HasError = false;
    }

    private void SetError(string message, string fallbackMessage)
    {
        ErrorMessage = string.IsNullOrWhiteSpace(message)
            ? fallbackMessage
            : message;
        HasError = true;
    }
}
