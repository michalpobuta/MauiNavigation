namespace MauiNavigation.Core.Services;

/// <summary>
/// Singleton service that holds the current filter criteria.
/// Implements IFilterService for proper abstraction and event support.
/// FilterViewModel writes to it on Apply; BrowseViewModel reads it on appear.
/// </summary>
public class MovieFilterState : IFilterService
{
    private FilterState _currentFilter = FilterState.Empty;

    // Legacy properties for backward compatibility
    public string? Genre
    {
        get => _currentFilter.Genre;
        set => ApplyFilter(value, MinYear);
    }

    public int? MinYear
    {
        get => _currentFilter.MinYear;
        set => ApplyFilter(Genre, value);
    }

    // IFilterService implementation
    public FilterState CurrentFilter => _currentFilter;

    public bool HasFilter => _currentFilter.HasFilter;

    public event EventHandler<FilterChangedEventArgs>? FilterChanged;

    public void ApplyFilter(string? genre, int? minYear)
    {
        var oldFilter = _currentFilter;
        var newFilter = new FilterState(genre, minYear);

        if (oldFilter == newFilter)
            return;

        _currentFilter = newFilter;
        FilterChanged?.Invoke(this, new FilterChangedEventArgs(oldFilter, newFilter));
    }

    public void ClearFilter()
    {
        if (!_currentFilter.HasFilter)
            return;

        var oldFilter = _currentFilter;
        _currentFilter = FilterState.Empty;
        FilterChanged?.Invoke(this, new FilterChangedEventArgs(oldFilter, FilterState.Empty));
    }

    [Obsolete("Use ClearFilter() instead")]
    public void Clear() => ClearFilter();
}
