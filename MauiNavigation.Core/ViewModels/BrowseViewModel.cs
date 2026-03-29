using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MauiNavigation.Core.Base;
using MauiNavigation.Core.Models;
using MauiNavigation.Core.Navigation;
using MauiNavigation.Core.Navigation.Parameters;
using MauiNavigation.Core.Services;
using Microsoft.Extensions.Logging;

namespace MauiNavigation.Core.ViewModels;

public partial class BrowseViewModel : BaseViewModel<BrowseViewModel>
{
    private readonly IFilterService _filterService;

    private static readonly Movie[] SampleMovies =
    [
        new(1, "The Shawshank Redemption", "Drama", 1994, "Two imprisoned men bond over years, finding solace and eventual redemption through acts of common decency."),
        new(2, "The Godfather", "Crime", 1972, "The aging patriarch of an organized crime dynasty transfers control of his empire to his reluctant son."),
        new(3, "Inception", "Sci-Fi", 2010, "A thief who steals corporate secrets through dream-sharing technology is given the task of planting an idea."),
        new(4, "Interstellar", "Sci-Fi", 2014, "A team of explorers travel through a wormhole in space in an attempt to ensure humanity's survival."),
        new(5, "Pulp Fiction", "Crime", 1994, "The lives of two mob hitmen, a boxer, a gangster and his wife intertwine in tales of violence and redemption."),
    ];

    public BrowseViewModel(BaseViewModelFacade facade, IFilterService filterService) : base(facade)
    {
        _filterService = filterService;

        // Subscribe to filter changes for reactive updates
        // This pattern is useful when you want immediate UI updates without relying on OnAppearingAsync
        _filterService.FilterChanged += OnFilterChanged;
    }

    public ObservableCollection<Movie> Movies { get; } = [];

    [ObservableProperty]
    private string _filterSummary = "No filter";

    protected override Task OnAppearingAsync(CancellationToken cancellationToken)
    {
        // Apply filtering when page appears
        // This handles the case when returning from modal or other screens
        ApplyCurrentFilter();
        return Task.CompletedTask;
    }

    /// <summary>
    /// Handles filter changes reactively (event-driven approach).
    /// Useful for scenarios where filter can change while this page is visible.
    /// </summary>
    private void OnFilterChanged(object? sender, FilterChangedEventArgs e)
    {
        Facade.Logger.LogInformation(
            "Filter changed: {OldGenre}/{OldYear} → {NewGenre}/{NewYear}",
            e.OldFilter.Genre, e.OldFilter.MinYear,
            e.NewFilter.Genre, e.NewFilter.MinYear);

        // Re-apply filter when it changes
        ApplyCurrentFilter();
    }

    private void ApplyCurrentFilter()
    {
        var filter = _filterService.CurrentFilter;
        var filtered = filter.HasFilter
            ? SampleMovies.Where(m =>
                (filter.Genre is null || m.Genre == filter.Genre) &&
                (filter.MinYear is null || m.Year >= filter.MinYear))
            : SampleMovies;

        Movies.Clear();
        foreach (var movie in filtered)
            Movies.Add(movie);

        // Update filter summary for UI
        FilterSummary = filter.HasFilter
            ? $"Filtered: {filter.Genre ?? "Any genre"}, {filter.MinYear?.ToString() ?? "Any year"}+"
            : "No filter";
    }

    public override void Dispose()
    {
        // Unsubscribe from events to prevent memory leaks
        _filterService.FilterChanged -= OnFilterChanged;
        base.Dispose();
    }

    [RelayCommand]
    private Task SelectMovie(Movie movie) =>
        Facade.Navigation.GoToAsync(Routes.MovieDetail, new MovieDetailParameters(movie.Id, movie.Title));

    /// <summary>
    /// Opens filter modal using fire-and-forget pattern.
    /// Filter changes are applied via the FilterChanged event (reactive pattern).
    /// This is the simpler approach — use when you don't need immediate feedback.
    /// </summary>
    [RelayCommand]
    private Task OpenFilter() =>
        Facade.Navigation.PresentModalAsync(Routes.Filter);

    /// <summary>
    /// Opens filter modal and awaits result using the result pattern.
    /// This approach gives immediate access to the filter result without relying on events.
    /// Use when you need to react to the result immediately or conditionally.
    /// </summary>
    [RelayCommand]
    private void OpenFilterWithResult()
    {
        SafeFireAndForget(async ct =>
        {
            var result = await Facade.Navigation.PresentModalForResultAsync<FilterResult>(Routes.Filter);

            if (result.TryGetValue(out var filter))
            {
                // User applied a filter — we get the result immediately
                Facade.Logger.LogInformation("Filter result received: Genre={Genre}, MinYear={MinYear}",
                    filter?.Genre, filter?.MinYear);

                // Show toast confirmation
                await Facade.Alerts.ShowToastAsync("Filter applied");

                // Note: The filter is already applied via the FilterChanged event,
                // but we have access to the result here for additional logic
            }
            else
            {
                Facade.Logger.LogInformation("Filter cancelled by user");
            }
        });
    }
}
