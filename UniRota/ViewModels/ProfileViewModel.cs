using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using UniRota.Services.Interfaces;

namespace UniRota.ViewModels;

public partial class ProfileViewModel : ObservableObject
{
    private readonly IAuthService _authService;

    [ObservableProperty]
    private string name = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotBusy))]
    private bool isBusy;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string errorMessage = string.Empty;

    public bool IsNotBusy => !IsBusy;
    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    public ProfileViewModel(IAuthService authService) => _authService = authService;

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (_authService.CurrentUser is not { } user)
        {
            await Shell.Current.GoToAsync("//login");
            return;
        }
        Name = user.Name;
        ErrorMessage = string.Empty;
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
