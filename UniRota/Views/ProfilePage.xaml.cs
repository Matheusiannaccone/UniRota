using UniRota.ViewModels;

namespace UniRota.Views;

public partial class ProfilePage : ContentPage
{
    private readonly ProfileViewModel _viewModel;

    public ProfilePage(ProfileViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadCommand.ExecuteAsync(null);
    }

    protected override void OnDisappearing()
    {
        _viewModel.ClearSensitiveFields();
        base.OnDisappearing();
    }

    protected override bool OnBackButtonPressed() => _viewModel.IsBusy || base.OnBackButtonPressed();
}
