using System.ComponentModel;
using UniRota.Models;
using UniRota.ViewModels;

namespace UniRota.Views.Routes;

public partial class NewRoutePage : ContentPage, IQueryAttributable
{
    public const string RouteParameterName = "Route";

    private readonly NewRouteViewModel _viewModel;
    private bool _isReturningToRoutes;

    public NewRoutePage(NewRouteViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue(RouteParameterName, out var value)
            && value is WeeklyRoute route)
        {
            _viewModel.BeginEdit(route);
            return;
        }

        _viewModel.BeginCreate();
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
