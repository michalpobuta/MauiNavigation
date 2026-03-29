using MauiNavigation.Core.Navigation;
using MauiNavigation.Core.Navigation.Parameters;
using Xunit;

namespace MauiNavigation.Tests.Navigation;

public class NavigationResultTests
{
    [Fact]
    public void Success_ReturnsSucceededTrue()
    {
        var result = NavigationResult<FilterResult>.Success(new FilterResult("Drama", 2000));

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Value);
        Assert.Equal("Drama", result.Value.Genre);
    }

    [Fact]
    public void Cancelled_ReturnsSucceededFalse()
    {
        var result = NavigationResult<FilterResult>.Cancelled();

        Assert.False(result.Succeeded);
        Assert.Null(result.Value);
    }

    [Fact]
    public void TryGetValue_ReturnsTrueForSuccess()
    {
        var expected = new FilterResult("Sci-Fi", 2010);
        var result = NavigationResult<FilterResult>.Success(expected);

        Assert.True(result.TryGetValue(out var value));
        Assert.Equal(expected, value);
    }

    [Fact]
    public void TryGetValue_ReturnsFalseForCancelled()
    {
        var result = NavigationResult<FilterResult>.Cancelled();

        Assert.False(result.TryGetValue(out var value));
        Assert.Null(value);
    }
}
