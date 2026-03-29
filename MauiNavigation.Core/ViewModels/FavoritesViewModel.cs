using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using MauiNavigation.Core.Base;
using MauiNavigation.Core.Models;
using MauiNavigation.Core.Navigation;
using MauiNavigation.Core.Navigation.Parameters;

namespace MauiNavigation.Core.ViewModels;

public partial class FavoritesViewModel(BaseViewModelFacade facade) : BaseViewModel<FavoritesViewModel>(facade)
{
    // In a real app, favorites would be persisted and loaded here.
    // For this demo the list stays empty to keep focus on navigation patterns.
    public ObservableCollection<Movie> Favorites { get; } = [];

    /// <summary>
    /// Example: Switch to the Browse tab when user wants to find more movies.
    /// Uses absolute navigation (//route) to jump across tabs.
    /// </summary>
    [RelayCommand]
    private Task GoToBrowse() => Facade.Navigation.SwitchTabAsync(Routes.BrowseTab);

    /// <summary>
    /// Example: Switch to Browse tab AND navigate directly to a specific movie.
    /// Combines tab switch + page push in one operation.
    /// </summary>
    [RelayCommand]
    private Task GoToFeaturedMovie()
    {
        // In a real app, this might be a "featured movie" or "continue watching" action
        var featuredMovie = new MovieDetailParameters(1, "The Shawshank Redemption");
        return Facade.Navigation.SwitchTabAndNavigateAsync(Routes.BrowseTab, Routes.MovieDetail, featuredMovie);
    }
}
