using Microsoft.Extensions.DependencyInjection;

namespace MauiDbManager;

public partial class App : Application
{
    private readonly IServiceProvider _services;

    public App(IServiceProvider services)
    {
        InitializeComponent();
        _services = services;
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        // Resolve the page only after InitializeComponent has loaded App.xaml.
        // MainPage uses colors and styles stored in the application resources.
        var mainPage = _services.GetRequiredService<MainPage>();

        return new(mainPage)
        {
            Title = "MAUI DB Manager",
            MinimumWidth = 900,
            MinimumHeight = 700
        };
    }
}
