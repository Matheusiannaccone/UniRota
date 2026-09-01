using Microsoft.Extensions.Logging;

using Microsoft.Maui.Storage;
using UniRota.Services.Firebase;
using UniRota.Services.Interfaces;

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
                ApiKey = string.Empty,
                ProjectId = string.Empty
            });
            builder.Services.AddSingleton(new HttpClient());
            builder.Services.AddSingleton<ISecureStorage>(SecureStorage.Default);
            builder.Services.AddSingleton<IAuthService, FirebaseAuthService>();

            return builder.Build();
        }
    }
}
