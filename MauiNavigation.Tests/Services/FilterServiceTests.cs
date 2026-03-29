using MauiNavigation.Core.Services;
using Xunit;

namespace MauiNavigation.Tests.Services;

public class FilterServiceTests
{
    [Fact]
    public void HasFilter_ReturnsFalseWhenEmpty()
    {
        var service = new FilterService();

        Assert.False(service.HasFilter);
        Assert.Equal(FilterState.Empty, service.CurrentFilter);
    }

    [Fact]
    public void ApplyFilter_SetsCurrentFilter()
    {
        var service = new FilterService();

        service.ApplyFilter("Drama", 2000);

        Assert.True(service.HasFilter);
        Assert.Equal("Drama", service.CurrentFilter.Genre);
        Assert.Equal(2000, service.CurrentFilter.MinYear);
    }

    [Fact]
    public void ApplyFilter_RaisesFilterChangedEvent()
    {
        var service = new FilterService();
        FilterChangedEventArgs? capturedArgs = null;
        service.FilterChanged += (_, args) => capturedArgs = args;

        service.ApplyFilter("Sci-Fi", 2010);

        Assert.NotNull(capturedArgs);
        Assert.Equal(FilterState.Empty, capturedArgs.OldFilter);
        Assert.Equal("Sci-Fi", capturedArgs.NewFilter.Genre);
        Assert.Equal(2010, capturedArgs.NewFilter.MinYear);
    }

    [Fact]
    public void ApplyFilter_DoesNotRaiseEvent_WhenValueUnchanged()
    {
        var service = new FilterService();
        service.ApplyFilter("Drama", 2000);

        int callCount = 0;
        service.FilterChanged += (_, _) => callCount++;

        service.ApplyFilter("Drama", 2000); // Same values

        Assert.Equal(0, callCount);
    }

    [Fact]
    public void ClearFilter_RemovesFilter()
    {
        var service = new FilterService();
        service.ApplyFilter("Drama", 2000);

        service.ClearFilter();

        Assert.False(service.HasFilter);
        Assert.Equal(FilterState.Empty, service.CurrentFilter);
    }

    [Fact]
    public void ClearFilter_RaisesFilterChangedEvent()
    {
        var service = new FilterService();
        service.ApplyFilter("Drama", 2000);

        FilterChangedEventArgs? capturedArgs = null;
        service.FilterChanged += (_, args) => capturedArgs = args;

        service.ClearFilter();

        Assert.NotNull(capturedArgs);
        Assert.Equal("Drama", capturedArgs.OldFilter.Genre);
        Assert.Equal(FilterState.Empty, capturedArgs.NewFilter);
    }

    [Fact]
    public void ClearFilter_DoesNotRaiseEvent_WhenAlreadyEmpty()
    {
        var service = new FilterService();

        int callCount = 0;
        service.FilterChanged += (_, _) => callCount++;

        service.ClearFilter();

        Assert.Equal(0, callCount);
    }

    [Fact]
    public void FilterState_HasFilterProperty()
    {
        var empty = FilterState.Empty;
        var withGenre = new FilterState("Drama", null);
        var withYear = new FilterState(null, 2000);
        var withBoth = new FilterState("Drama", 2000);

        Assert.False(empty.HasFilter);
        Assert.True(withGenre.HasFilter);
        Assert.True(withYear.HasFilter);
        Assert.True(withBoth.HasFilter);
    }
}
