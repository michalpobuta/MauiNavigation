using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MauiNavigation.Core.Base;
using MauiNavigation.Core.Navigation;
using MauiNavigation.Core.Navigation.Parameters;
using MauiNavigation.Core.Services;

namespace MauiNavigation.Core.ViewModels;

/// <summary>
/// ViewModel for the Filter modal.
/// Implements IModalResultProvider&lt;FilterResult&gt; to support result-returning navigation.
/// Implements INavigationGuard to confirm discard when user has unsaved changes.
/// Can be used with both fire-and-forget (PresentModalAsync) or result-awaiting (PresentModalForResultAsync).
/// </summary>
public partial class FilterViewModel : BaseViewModel<FilterViewModel>, IModalResultProvider<FilterResult>, INavigationGuard
{
    private readonly IFilterService _filterService;
    private TaskCompletionSource<NavigationResult<FilterResult>>? _resultTcs;

    // Track initial values to detect changes
    private string? _initialGenre;
    private string? _initialMinYear;

    public FilterViewModel(BaseViewModelFacade facade, IFilterService filterService) : base(facade)
    {
        _filterService = filterService;
    }

    [ObservableProperty]
    private string? _genre;

    [ObservableProperty]
    private string? _minYear; // String for easy Entry binding; parse to int when applying

    public override Task InitializeFromQueryAsync(IDictionary<string, object> query, bool isGoBack, CancellationToken ct)
    {
        // Pre-populate from current filter state so the user sees their last filter.
        var currentFilter = _filterService.CurrentFilter;
        Genre = currentFilter.Genre;
        MinYear = currentFilter.MinYear?.ToString();

        // Override with any explicitly passed parameters.
        if (query.TryGetValue("__params", out var p) && p is FilterParameters parms)
        {
            Genre = parms.Genre;
            MinYear = parms.MinYear?.ToString();
        }

        // Store initial values for change detection
        _initialGenre = Genre;
        _initialMinYear = MinYear;

        return Task.CompletedTask;
    }

    #region INavigationGuard

    /// <summary>
    /// Returns true if the filter values have changed since the modal was opened.
    /// </summary>
    public bool HasUnsavedChanges =>
        Genre != _initialGenre || MinYear != _initialMinYear;

    /// <summary>
    /// Shows a confirmation dialog when the user tries to dismiss with unsaved changes.
    /// </summary>
    public async Task<bool> CanLeaveAsync()
    {
        if (!HasUnsavedChanges)
            return true;

        return await Facade.Alerts.ShowConfirmAsync(
            "Discard Changes?",
            "You have unsaved filter changes. Discard them?",
            "Discard",
            "Keep Editing");
    }

    #endregion

    #region IModalResultProvider<FilterResult>

    public void SetResultCompletion(TaskCompletionSource<NavigationResult<FilterResult>> tcs)
        => _resultTcs = tcs;

    public void CompleteWithResult(FilterResult result)
    {
        _resultTcs?.TrySetResult(NavigationResult<FilterResult>.Success(result));
        _resultTcs = null;
    }

    public void CompleteAsCancelled()
    {
        _resultTcs?.TrySetResult(NavigationResult<FilterResult>.Cancelled());
        _resultTcs = null;
    }

    #endregion

    [RelayCommand]
    private async Task Apply()
    {
        var genre = string.IsNullOrWhiteSpace(Genre) ? null : Genre.Trim();
        var minYear = int.TryParse(MinYear, out var y) ? y : (int?)null;

        // Update shared state via IFilterService (triggers FilterChanged event)
        _filterService.ApplyFilter(genre, minYear);

        // Update initial values so HasUnsavedChanges returns false
        _initialGenre = Genre;
        _initialMinYear = MinYear;

        // Complete result for await-based callers
        CompleteWithResult(new FilterResult(genre, minYear));

        await Facade.Navigation.DismissModalAsync();
    }

    [RelayCommand]
    private async Task Dismiss()
    {
        // Note: Navigation guard will be checked by DismissModalAsync if this
        // ViewModel is on a non-modal page. For modals, we handle it here:
        if (HasUnsavedChanges)
        {
            var discard = await Facade.Alerts.ShowConfirmAsync(
                "Discard Changes?",
                "You have unsaved filter changes. Discard them?",
                "Discard",
                "Keep Editing");

            if (!discard)
                return;
        }

        CompleteAsCancelled();
        await Facade.Navigation.DismissModalAsync();
    }

    [RelayCommand]
    private async Task Clear()
    {
        Genre = null;
        MinYear = null;

        // Clear shared state via IFilterService
        _filterService.ClearFilter();

        // Update initial values
        _initialGenre = null;
        _initialMinYear = null;

        // Complete with empty result
        CompleteWithResult(new FilterResult(null, null));

        await Facade.Navigation.DismissModalAsync();
    }
}
