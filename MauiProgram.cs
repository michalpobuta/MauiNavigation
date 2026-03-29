using CommunityToolkit.Maui;
using Microsoft.Extensions.Logging;
using MauiNavigation.Base;
using MauiNavigation.Core.Base;
using MauiNavigation.Core.Navigation;
using MauiNavigation.Core.ViewModels;
using MauiNavigation.Core.Services;
using MauiNavigation.Infrastructure;
using MauiNavigation.Pages.Browse;
using MauiNavigation.Pages.Favorites;
using MauiNavigation.Pages.MovieDetail;
using MauiNavigation.Pages.Filter;

namespace MauiNavigation;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseMauiCommunityToolkit()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        // Infrastructure — singletons live for the app lifetime
        builder.Services.AddSingleton<INavigationService, ShellNavigationService>();
        builder.Services.AddSingleton<IAlertService, AlertService>();
        builder.Services.AddSingleton<IDeepLinkHandler, DeepLinkHandler>();
        builder.Services.AddSingleton<IFilterService, MovieFilterState>(); // Or use FilterService
        builder.Services.AddSingleton<BaseViewModelFacade>();
        builder.Services.AddSingleton<AppShell>();

        // ViewModels — Transient: fresh instance per navigation push
        // Transient required — BrowseViewModel captures a Singleton (MovieFilterState);
        // registering as Singleton would hold stale filter state across app sessions.
        builder.Services.AddTransient<BrowseViewModel>();
        builder.Services.AddTransient<FavoritesViewModel>();
        builder.Services.AddTransient<MovieDetailViewModel>();
        builder.Services.AddTransient<FilterViewModel>();

        // Pages — Transient: lifetime must match ViewModel
        builder.Services.AddTransient<BrowsePage>();
        builder.Services.AddTransient<FavoritesPage>();
        builder.Services.AddTransient<MovieDetailPage>();
        builder.Services.AddTransient<FilterPage>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
