using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using UniRota.Services.Interfaces;

namespace UniRota.ViewModels;

public partial class StartupViewModel : ObservableObject
{
    private readonly IAuthService _authService;

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private string statusMessage = "Verificando sua sessão...";

    [ObservableProperty]
    private string errorMessage = string.Empty;

    [ObservableProperty]
    private bool hasError;

    public StartupViewModel(IAuthService authService)
    {
        _authService = authService;
    }

    public bool IsNotBusy => !IsBusy;

    partial void OnIsBusyChanged(bool value)
    {
        OnPropertyChanged(nameof(IsNotBusy));
    }

    [RelayCommand(AllowConcurrentExecutions = false)]
    private async Task InitializeAsync(CancellationToken cancellationToken)
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        ClearError();
        StatusMessage = "Verificando sua sessão...";

        try
        {
            var user = await _authService.RestoreSessionAsync(cancellationToken);
            await Shell.Current.GoToAsync(user is null ? "//login" : "//home");
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

    private void ClearError()
    {
        ErrorMessage = string.Empty;
        HasError = false;
    }

    private void SetError(string message)
    {
        ErrorMessage = string.IsNullOrWhiteSpace(message)
            ? "Não foi possível verificar sua sessão. Tente novamente."
            : message;
        HasError = true;
    }
}
