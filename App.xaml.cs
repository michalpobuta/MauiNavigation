using MauiNavigation.Core.Navigation;

namespace MauiNavigation;

public partial class App : Application
{
    private readonly IDeepLinkHandler _deepLinkHandler;

    public App(IServiceProvider services, IDeepLinkHandler deepLinkHandler)
    {
        InitializeComponent();
        _deepLinkHandler = deepLinkHandler;
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        var window = new Window(Handler?.MauiContext?.Services.GetRequiredService<AppShell>());
        return window;
    }

    /// <summary>
    /// Called when the app receives an app link (deep link).
    /// On Android: Intent with VIEW action and matching intent-filter
    /// On iOS: Universal Link or custom URL scheme
    /// </summary>
    protected override async void OnAppLinkRequestReceived(Uri uri)
    {
        base.OnAppLinkRequestReceived(uri);

        if (_deepLinkHandler.CanHandle(uri))
        {
            // Delay slightly to ensure Shell is ready
            await Task.Delay(100);
            await _deepLinkHandler.HandleAsync(uri);
        }
    }
}