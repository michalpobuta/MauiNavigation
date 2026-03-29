using MauiNavigation.Core.Navigation;
using MauiNavigation.Core.Services;

namespace MauiNavigation.Tests.Mocks;

/// <summary>
/// Mock implementation of INavigationService for testing.
/// Records all navigation calls for verification.
/// </summary>
public class MockNavigationService : INavigationService
{
    private readonly List<NavigationCall> _calls = [];
    private readonly Dictionary<string, Type> _modalRegistry = new();

    public IReadOnlyList<NavigationCall> Calls => _calls;

    public void Reset() => _calls.Clear();

    // Configure results for PresentModalForResultAsync
    private readonly Dictionary<string, object> _modalResults = new();

    public void SetModalResult<TResult>(string route, NavigationResult<TResult> result)
        => _modalResults[route] = result;

    public void RegisterModal(string route, Type pageType)
        => _modalRegistry[route] = pageType;

    public Task GoToAsync(string route, bool animated = true)
    {
        _calls.Add(new NavigationCall(NavigationType.GoTo, route, null, animated));
        return Task.CompletedTask;
    }

    public Task GoToAsync<TParams>(string route, TParams parameters, bool animated = true) where TParams : class
    {
        _calls.Add(new NavigationCall(NavigationType.GoTo, route, parameters, animated));
        return Task.CompletedTask;
    }

    public Task GoBackAsync(bool animated = true)
    {
        _calls.Add(new NavigationCall(NavigationType.GoBack, "..", null, animated));
        return Task.CompletedTask;
    }

    public Task GoBackAsync(int levels, bool animated = true)
    {
        var route = string.Join("/", Enumerable.Repeat("..", levels));
        _calls.Add(new NavigationCall(NavigationType.GoBack, route, null, animated));
        return Task.CompletedTask;
    }

    public Task GoBackToRootAsync(bool animated = true)
    {
        _calls.Add(new NavigationCall(NavigationType.GoBackToRoot, "//root", null, animated));
        return Task.CompletedTask;
    }

    public Task PresentModalAsync(string route, bool animated = true)
    {
        _calls.Add(new NavigationCall(NavigationType.PresentModal, route, null, animated));
        return Task.CompletedTask;
    }

    public Task PresentModalAsync<TParams>(string route, TParams parameters, bool animated = true) where TParams : class
    {
        _calls.Add(new NavigationCall(NavigationType.PresentModal, route, parameters, animated));
        return Task.CompletedTask;
    }

    public Task DismissModalAsync(bool animated = true)
    {
        _calls.Add(new NavigationCall(NavigationType.DismissModal, null, null, animated));
        return Task.CompletedTask;
    }

    public Task<NavigationResult<TResult>> PresentModalForResultAsync<TResult>(string route, bool animated = true)
    {
        _calls.Add(new NavigationCall(NavigationType.PresentModalForResult, route, null, animated));

        if (_modalResults.TryGetValue(route, out var result) && result is NavigationResult<TResult> typedResult)
            return Task.FromResult(typedResult);

        return Task.FromResult(NavigationResult<TResult>.Cancelled());
    }

    public Task<NavigationResult<TResult>> PresentModalForResultAsync<TParams, TResult>(
        string route, TParams parameters, bool animated = true) where TParams : class
    {
        _calls.Add(new NavigationCall(NavigationType.PresentModalForResult, route, parameters, animated));

        if (_modalResults.TryGetValue(route, out var result) && result is NavigationResult<TResult> typedResult)
            return Task.FromResult(typedResult);

        return Task.FromResult(NavigationResult<TResult>.Cancelled());
    }

    public Task SwitchTabAsync(string tabRoute, bool animated = true)
    {
        _calls.Add(new NavigationCall(NavigationType.SwitchTab, tabRoute, null, animated));
        return Task.CompletedTask;
    }

    public Task SwitchTabAndNavigateAsync<TParams>(string tabRoute, string pageRoute, TParams parameters, bool animated = true)
        where TParams : class
    {
        _calls.Add(new NavigationCall(NavigationType.SwitchTabAndNavigate, $"{tabRoute}/{pageRoute}", parameters, animated));
        return Task.CompletedTask;
    }
}

public enum NavigationType
{
    GoTo,
    GoBack,
    GoBackToRoot,
    PresentModal,
    DismissModal,
    PresentModalForResult,
    SwitchTab,
    SwitchTabAndNavigate
}

public record NavigationCall(NavigationType Type, string? Route, object? Parameters, bool Animated);
