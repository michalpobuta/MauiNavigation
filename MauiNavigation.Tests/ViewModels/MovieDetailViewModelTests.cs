using MauiNavigation.Core.Base;
using MauiNavigation.Core.Navigation;
using MauiNavigation.Core.Navigation.Parameters;
using MauiNavigation.Core.ViewModels;
using MauiNavigation.Tests.Mocks;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace MauiNavigation.Tests.ViewModels;

public class MovieDetailViewModelTests
{
    private static BaseViewModelFacade CreateFacade() => new(
        Substitute.For<INavigationService>(),
        NullAlertService.Instance,
        NullLogger<BaseViewModelFacade>.Instance);

    [Fact]
    public async Task InitializeFromQueryAsync_SetsTitle_FromMovieDetailParameters()
    {
        var vm = new MovieDetailViewModel(CreateFacade());
        var query = new Dictionary<string, object>
        {
            ["__params"] = new MovieDetailParameters(1, "Inception")
        };

        await vm.InitializeFromQueryAsync(query, isGoBack: false, CancellationToken.None);

        Assert.Equal("Inception", vm.Title);
    }

    [Fact]
    public async Task InitializeFromQueryAsync_DoesNotOverwrite_WhenIsGoBack()
    {
        var vm = new MovieDetailViewModel(CreateFacade());
        vm.Title = "Already Set";
        var query = new Dictionary<string, object>
        {
            ["__params"] = new MovieDetailParameters(1, "Inception")
        };

        await vm.InitializeFromQueryAsync(query, isGoBack: true, CancellationToken.None);

        Assert.Equal("Already Set", vm.Title);
    }
}
