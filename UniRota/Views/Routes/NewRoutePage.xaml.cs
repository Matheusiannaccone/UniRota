using System.ComponentModel;
#if ANDROID
using Android.Text;
using Android.Text.Method;
#endif
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

#if ANDROID
        EstimatedDistanceEntry.HandlerChanged +=
            OnEstimatedDistanceEntryHandlerChanged;
        ConfigureEstimatedDistanceKeyboard();
#endif
    }

#if ANDROID
    private void OnEstimatedDistanceEntryHandlerChanged(
        object? sender,
        EventArgs e)
    {
        ConfigureEstimatedDistanceKeyboard();
    }

    private void ConfigureEstimatedDistanceKeyboard()
    {
        if (EstimatedDistanceEntry.Handler?.PlatformView
            is Android.Widget.EditText platformEntry)
        {
            platformEntry.KeyListener = new PtBrDecimalKeyListener();
        }
    }
#endif

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

#if ANDROID
    private sealed class PtBrDecimalKeyListener : NumberKeyListener
    {
        private static readonly char[] AcceptedCharacters =
            "0123456789,".ToCharArray();

        public override InputTypes InputType =>
            InputTypes.ClassNumber | InputTypes.NumberFlagDecimal;

        protected override char[] GetAcceptedChars() => AcceptedCharacters;
    }
#endif
}
