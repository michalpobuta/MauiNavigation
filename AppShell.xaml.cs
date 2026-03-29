using MauiNavigation.Core.Navigation;
using MauiNavigation.Pages.MovieDetail;
using MauiNavigation.Pages.Filter;

namespace MauiNavigation;

public partial class AppShell : Shell
{
    public AppShell(INavigationService navigationService)
    {
        InitializeComponent();

        // Push routes — Shell resolves these via GoToAsync(nameof(...))
        // nameof(MovieDetailPage) == Routes.MovieDetail == "MovieDetailPage"
        Routing.RegisterRoute(nameof(MovieDetailPage), typeof(MovieDetailPage));

        // Modal routes — bypass Shell routing, resolved by ShellNavigationService
        // nameof(FilterPage) == Routes.Filter == "FilterPage"
        navigationService.RegisterModal(nameof(FilterPage), typeof(FilterPage));
    }
}
