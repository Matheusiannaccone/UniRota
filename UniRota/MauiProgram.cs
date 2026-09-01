using Microsoft.Extensions.Logging;

using Microsoft.Maui.Storage;
using UniRota.Services.Firebase;
using UniRota.Services.Interfaces;
using UniRota.ViewModels;
using UniRota.Views;
using UniRota.Views.Auth;

namespace UniRota
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                });

#if DEBUG
    		builder.Logging.AddDebug();
#endif

            // Preencha com os valores do Firebase Console antes de usar a autenticação.
            builder.Services.AddSingleton(new FirebaseOptions
            {
                ApiKey = "AIzaSyAzpxdWWh1ZnpYW1L8tREBbLvywpUPzUvc",
                ProjectId = "unirota-f0a63"
            });
            builder.Services.AddSingleton(new HttpClient());
            builder.Services.AddSingleton<ISecureStorage>(SecureStorage.Default);
            builder.Services.AddSingleton<IAuthService, FirebaseAuthService>();
            builder.Services.AddSingleton<IRouteService, FirebaseRouteService>();

            builder.Services.AddSingleton<AppShell>();
            builder.Services.AddTransient<StartupViewModel>();
            builder.Services.AddTransient<LoginViewModel>();
            builder.Services.AddTransient<RegisterViewModel>();
            builder.Services.AddTransient<HomeViewModel>();
            builder.Services.AddTransient<NewRouteViewModel>();
            builder.Services.AddTransient<MyRoutesViewModel>();
            builder.Services.AddTransient<StartupPage>();
            builder.Services.AddTransient<LoginPage>();
            builder.Services.AddTransient<RegisterPage>();
            builder.Services.AddTransient<HomePage>();

            return builder.Build();
        }
    }
}
