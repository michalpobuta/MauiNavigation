using CommunityToolkit.Mvvm.Input;
using MauiNavigation.Core.Base;
using MauiNavigation.Core.Models;
using MauiNavigation.Core.Navigation;
using MauiNavigation.Core.Navigation.Parameters;
using MauiNavigation.Core.Services;
using MauiNavigation.Core.ViewModels;
using MauiNavigation.Tests.Mocks;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace MauiNavigation.Tests.ViewModels;

public class BrowseViewModelTests
{
    private static BaseViewModelFacade CreateFacade(INavigationService? nav = null) => new(
        nav ?? Substitute.For<INavigationService>(),
        NullAlertService.Instance,
        NullLogger<BaseViewModelFacade>.Instance);

    private static IFilterService CreateFilterService() => new MovieFilterState();

    [Fact]
    public async Task OnAppearingAsync_PopulatesMovies()
    {
        var vm = new BrowseViewModel(CreateFacade(), CreateFilterService());
        await vm.OnAppearingInternal();
        Assert.NotEmpty(vm.Movies);
    }

    [Fact]
    public async Task OnAppearingAsync_CalledTwice_DoesNotDuplicateMovies()
    {
        var vm = new BrowseViewModel(CreateFacade(), CreateFilterService());
        await vm.OnAppearingInternal();
        var count = vm.Movies.Count;
        await vm.OnDisappearingInternal();
        await vm.OnAppearingInternal();
        Assert.Equal(count, vm.Movies.Count);
    }

    [Fact]
    public async Task SelectMovieCommand_NavigatesToMovieDetail_WithCorrectParameters()
    {
        var nav = Substitute.For<INavigationService>();
        var vm = new BrowseViewModel(CreateFacade(nav), CreateFilterService());
        await vm.OnAppearingInternal();
        var movie = vm.Movies.First();

        await ((AsyncRelayCommand<Movie>)vm.SelectMovieCommand).ExecuteAsync(movie);

        await nav.Received(1).GoToAsync(
            Routes.MovieDetail,
            Arg.Is<MovieDetailParameters>(p => p.MovieId == movie.Id && p.Title == movie.Title),
            Arg.Any<bool>());
    }

    [Fact]
    public async Task OpenFilterCommand_PresentsFilterModal()
    {
        var nav = Substitute.For<INavigationService>();
        var vm = new BrowseViewModel(CreateFacade(nav), CreateFilterService());
        await vm.OnAppearingInternal();

        await ((AsyncRelayCommand)vm.OpenFilterCommand).ExecuteAsync(null);

        await nav.Received(1).PresentModalAsync(Routes.Filter, Arg.Any<bool>());
    }

    [Fact]
    public async Task FilterChanged_UpdatesMovieList()
    {
        var filterService = CreateFilterService();
        var vm = new BrowseViewModel(CreateFacade(), filterService);
        await vm.OnAppearingInternal();
        var initialCount = vm.Movies.Count;

        // Apply filter via service (triggers FilterChanged event)
        filterService.ApplyFilter("Sci-Fi", null);

        // Check that movies are filtered
        Assert.True(vm.Movies.Count < initialCount);
        Assert.All(vm.Movies, m => Assert.Equal("Sci-Fi", m.Genre));
    }
}
