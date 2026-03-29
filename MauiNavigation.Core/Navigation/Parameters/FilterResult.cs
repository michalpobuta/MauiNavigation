namespace MauiNavigation.Core.Navigation.Parameters;

/// <summary>
/// Result returned by the Filter modal when the user applies filters.
/// Returned via NavigationResult&lt;FilterResult&gt; from PresentModalForResultAsync.
/// </summary>
public record FilterResult(string? Genre, int? MinYear);
