using MauiNavigation.Core.Base;
using MauiNavigation.Core.Navigation;
using MauiNavigation.Core.Navigation.Parameters;
using MauiNavigation.Core.Services;
using MauiNavigation.Core.ViewModels;
using MauiNavigation.Tests.Mocks;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace MauiNavigation.Tests.ViewModels;

public class FilterViewModelTests
{
    private static BaseViewModelFacade CreateFacade(INavigationService? nav = null, IAlertService? alerts = null) => new(
        nav ?? Substitute.For<INavigationService>(),
        alerts ?? NullAlertService.Instance,
        NullLogger<BaseViewModelFacade>.Instance);

    private static IFilterService CreateFilterService(string? genre = null, int? minYear = null)
    {
        var service = new MovieFilterState();
        if (genre is not null || minYear is not null)
            service.ApplyFilter(genre, minYear);
        return service;
    }

    [Fact]
    public async Task InitializeFromQueryAsync_PrePopulatesFromFilterState()
    {
        var filterService = CreateFilterService("Drama", 2000);
        var vm = new FilterViewModel(CreateFacade(), filterService);

        await vm.InitializeFromQueryAsync(new Dictionary<string, object>(), isGoBack: false, CancellationToken.None);

        Assert.Equal("Drama", vm.Genre);
        Assert.Equal("2000", vm.MinYear);
    }

    [Fact]
    public async Task ApplyCommand_WritesGenreAndMinYearToFilterService_AndDismisses()
    {
        var nav = Substitute.For<INavigationService>();
        var filterService = CreateFilterService();
        var vm = new FilterViewModel(CreateFacade(nav), filterService);
        vm.Genre = "Sci-Fi";
        vm.MinYear = "1990";

        await vm.ApplyCommand.ExecuteAsync(null);

        Assert.Equal("Sci-Fi", filterService.CurrentFilter.Genre);
        Assert.Equal(1990, filterService.CurrentFilter.MinYear);
        await nav.Received(1).DismissModalAsync(Arg.Any<bool>());
    }

    [Fact]
    public async Task DismissCommand_DismissesWithoutWritingState()
    {
        var nav = Substitute.For<INavigationService>();
        var filterService = CreateFilterService();
        var vm = new FilterViewModel(CreateFacade(nav), filterService);

        // Initialize first (so HasUnsavedChanges can work)
        await vm.InitializeFromQueryAsync(new Dictionary<string, object>(), isGoBack: false, CancellationToken.None);

        // No changes made, so should dismiss without prompt
        await vm.DismissCommand.ExecuteAsync(null);

        Assert.Null(filterService.CurrentFilter.Genre);
        await nav.Received(1).DismissModalAsync(Arg.Any<bool>());
    }

    [Fact]
    public async Task HasUnsavedChanges_ReturnsTrueWhenGenreChanged()
    {
        var filterService = CreateFilterService();
        var vm = new FilterViewModel(CreateFacade(), filterService);

        await vm.InitializeFromQueryAsync(new Dictionary<string, object>(), isGoBack: false, CancellationToken.None);
        Assert.False(vm.HasUnsavedChanges);

        vm.Genre = "Action";
        Assert.True(vm.HasUnsavedChanges);
    }

    [Fact]
    public async Task ClearCommand_ClearsFilterService()
    {
        var nav = Substitute.For<INavigationService>();
        var filterService = CreateFilterService("Drama", 2000);
        var vm = new FilterViewModel(CreateFacade(nav), filterService);

        await vm.ClearCommand.ExecuteAsync(null);

        Assert.False(filterService.HasFilter);
        await nav.Received(1).DismissModalAsync(Arg.Any<bool>());
    }
}
