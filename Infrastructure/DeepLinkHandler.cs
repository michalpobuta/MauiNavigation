using MauiNavigation.Core.Navigation;
using MauiNavigation.Core.Navigation.Parameters;
using Microsoft.Extensions.Logging;

namespace MauiNavigation.Infrastructure;

/// <summary>
/// Handles deep links for the app.
/// 
/// Supported deep link formats:
/// - mauinavigation://movie/{id}        → Opens MovieDetailPage for the given movie ID
/// - mauinavigation://browse            → Opens the Browse tab
/// - mauinavigation://favorites         → Opens the Favorites tab
/// - https://mauinavigation.app/movie/{id} → Same as above (for App Links / Universal Links)
/// </summary>
public class DeepLinkHandler : IDeepLinkHandler
{
    private readonly INavigationService _navigation;
    private readonly ILogger<DeepLinkHandler> _logger;

    // Supported schemes and hosts
    private static readonly string[] SupportedSchemes = ["mauinavigation", "https", "http"];
    private static readonly string[] SupportedHosts = ["mauinavigation.app", ""];

    public DeepLinkHandler(INavigationService navigation, ILogger<DeepLinkHandler> logger)
    {
        _navigation = navigation;
        _logger = logger;
    }

    public bool CanHandle(Uri uri)
    {
        if (uri is null)
            return false;

        // Custom scheme: mauinavigation://
        if (uri.Scheme.Equals("mauinavigation", StringComparison.OrdinalIgnoreCase))
            return true;

        // App Links / Universal Links: https://mauinavigation.app/
        if ((uri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase) ||
             uri.Scheme.Equals("http", StringComparison.OrdinalIgnoreCase)) &&
            uri.Host.Equals("mauinavigation.app", StringComparison.OrdinalIgnoreCase))
            return true;

        return false;
    }

    public async Task<bool> HandleAsync(Uri uri)
    {
        if (!CanHandle(uri))
        {
            _logger.LogWarning("Deep link not handled: {Uri}", uri);
            return false;
        }

        _logger.LogInformation("Handling deep link: {Uri}", uri);

        // Parse path segments
        // For "mauinavigation://movie/42" → segments = ["movie", "42"]
        // For "https://mauinavigation.app/movie/42" → segments = ["movie", "42"]
        var segments = uri.AbsolutePath
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .ToList();

        // Handle custom scheme where host is the first segment
        // "mauinavigation://movie/42" has Host="movie" and AbsolutePath="/42"
        if (uri.Scheme.Equals("mauinavigation", StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrEmpty(uri.Host))
        {
            segments.Insert(0, uri.Host);
        }

        if (segments.Count == 0)
        {
            _logger.LogWarning("Deep link has no path: {Uri}", uri);
            return false;
        }

        var command = segments[0].ToLowerInvariant();

        return command switch
        {
            "movie" when segments.Count >= 2 && int.TryParse(segments[1], out var movieId)
                => await HandleMovieDeepLinkAsync(movieId),

            "browse" => await HandleBrowseDeepLinkAsync(),

            "favorites" => await HandleFavoritesDeepLinkAsync(),

            _ => HandleUnknownDeepLink(uri)
        };
    }

    private async Task<bool> HandleMovieDeepLinkAsync(int movieId)
    {
        _logger.LogInformation("Deep link: Opening movie {MovieId}", movieId);

        // Navigate to movie detail page
        // Title is unknown from deep link, will be loaded by the ViewModel
        var parameters = new MovieDetailParameters(movieId, $"Movie #{movieId}");
        await _navigation.GoToAsync(Routes.MovieDetail, parameters);
        return true;
    }

    private async Task<bool> HandleBrowseDeepLinkAsync()
    {
        _logger.LogInformation("Deep link: Switching to Browse tab");
        await _navigation.SwitchTabAsync(Routes.BrowseTab);
        return true;
    }

    private async Task<bool> HandleFavoritesDeepLinkAsync()
    {
        _logger.LogInformation("Deep link: Switching to Favorites tab");
        await _navigation.SwitchTabAsync(Routes.FavoritesTab);
        return true;
    }

    private bool HandleUnknownDeepLink(Uri uri)
    {
        _logger.LogWarning("Unknown deep link path: {Uri}", uri);
        return false;
    }
}
