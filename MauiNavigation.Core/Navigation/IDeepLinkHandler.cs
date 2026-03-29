namespace MauiNavigation.Core.Navigation;

/// <summary>
/// Interface for handling deep links (app links / universal links).
/// Implement in the App project to process incoming deep link URIs.
/// </summary>
public interface IDeepLinkHandler
{
    /// <summary>
    /// Handles an incoming deep link URI.
    /// Parse the URI and navigate to the appropriate page.
    /// Returns true if the link was handled, false if it was ignored.
    /// </summary>
    /// <param name="uri">The incoming deep link URI (e.g., "mauinavigation://movie/42")</param>
    Task<bool> HandleAsync(Uri uri);

    /// <summary>
    /// Checks if the given URI is a valid deep link for this app.
    /// </summary>
    bool CanHandle(Uri uri);
}
