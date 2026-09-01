using System.ComponentModel;
using UniRota.ViewModels;

namespace UniRota.Views.Routes;

public partial class NewRoutePage : ContentPage
{
    private readonly NewRouteViewModel _viewModel;
    private bool _isReturningToRoutes;

    public NewRoutePage(NewRouteViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
    }

    private async void OnViewModelPropertyChanged(
        object? sender,
        PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(NewRouteViewModel.HasSavedSuccessfully)
            || !_viewModel.HasSavedSuccessfully
            || _isReturningToRoutes)
        {
            return;
        }

        _isReturningToRoutes = true;

        try
        {
            await Shell.Current.GoToAsync("..");
        }
        catch (Exception)
        {
            _isReturningToRoutes = false;
            await DisplayAlert(
                "Rota salva",
                "A rota foi cadastrada, mas não foi possível voltar automaticamente.",
                "OK");
        }
    }
}
