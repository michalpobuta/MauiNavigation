namespace MauiNavigation.Core.Navigation;

/// <summary>
/// Route names matching the MAUI page class names.
/// AppShell registers these with Routing.RegisterRoute(nameof(PageType), ...)
/// and navigationService.RegisterModal(nameof(PageType), ...) — those strings
/// must equal the constants here.
/// </summary>
public static class Routes
{
    // Push routes (detail pages)
    public const string MovieDetail = "MovieDetailPage";

    // Modal routes
    public const string Filter = "FilterPage";

    // Tab routes — used with SwitchTabAsync for cross-tab navigation
    // These must match the Route attribute on ShellContent in AppShell.xaml
    public const string BrowseTab = "BrowsePage";
    public const string FavoritesTab = "FavoritesPage";
}
