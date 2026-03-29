namespace MauiNavigation.Core.Services;

/// <summary>
/// Singleton implementation of IFilterService.
/// Provides shared filter state with change notifications.
/// </summary>
public class FilterService : IFilterService
{
    private FilterState _currentFilter = FilterState.Empty;

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
}
