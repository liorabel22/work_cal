using MauiDbManager.Services;
using MauiDbManager.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace MauiDbManager;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();

        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts => { });

        // Register app parts once. MAUI's dependency-injection container creates
        // the service, view model, and page and supplies their constructor arguments.
        builder.Services.AddSingleton<IDatabaseConnectionService, SqlServerConnectionService>();
        builder.Services.AddSingleton<IDatabaseLookupService, SqlServerLookupService>();
        builder.Services.AddSingleton<IDatabaseModificationService, SqlServerModificationService>();
        builder.Services.AddTransient<ConnectionViewModel>();
        builder.Services.AddTransient<MainPage>();

        return builder.Build();
    }
}
