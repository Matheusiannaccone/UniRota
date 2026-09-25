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

    private void OnDayClicked(object sender, EventArgs e)
    {
        if (!_viewModel.IsBusy && sender is Button { CommandParameter: SelectableDayViewModel day })
            day.IsSelected = !day.IsSelected;
    }

    private void OnRoleCheckedChanged(object sender, CheckedChangedEventArgs e)
    {
        if (e.Value && sender is RadioButton { Value: RouteRoleOption role }
            && BindingContext is NewRouteViewModel viewModel && !viewModel.IsBusy)
            viewModel.SelectedRole = role;
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
