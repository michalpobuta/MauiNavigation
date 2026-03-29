namespace MauiNavigation.Core.Services;

/// <summary>
/// Interface for shared filter state across the app.
/// Implement as a Singleton to share filter state between pages.
/// </summary>
public interface IFilterService
{
    /// <summary>
    /// Gets the current filter state.
    /// </summary>
    FilterState CurrentFilter { get; }

    /// <summary>
    /// Returns true if any filter is active.
    /// </summary>
    bool HasFilter { get; }

    /// <summary>
    /// Applies a new filter. Raises FilterChanged event.
    /// </summary>
    void ApplyFilter(string? genre, int? minYear);

    /// <summary>
    /// Clears all filters. Raises FilterChanged event.
    /// </summary>
    void ClearFilter();

    /// <summary>
    /// Raised when the filter changes. Subscribe to react to filter changes.
    /// </summary>
    event EventHandler<FilterChangedEventArgs>? FilterChanged;
}

/// <summary>
/// Immutable record representing filter state.
/// </summary>
public record FilterState(string? Genre, int? MinYear)
{
    public static FilterState Empty => new(null, null);

    public bool HasFilter => Genre is not null || MinYear is not null;
}

/// <summary>
/// Event args for filter changes.
/// </summary>
public class FilterChangedEventArgs : EventArgs
{
    public FilterState OldFilter { get; }
    public FilterState NewFilter { get; }

    public FilterChangedEventArgs(FilterState oldFilter, FilterState newFilter)
    {
        OldFilter = oldFilter;
        NewFilter = newFilter;
    }
}
