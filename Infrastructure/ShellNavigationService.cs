using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Controls;
using MauiNavigation.Core.Navigation;

namespace MauiNavigation.Infrastructure;

public class ShellNavigationService(IServiceProvider serviceProvider) : INavigationService
{
    private readonly Dictionary<string, Type> _modalRegistry = new();

    /// <summary>
    /// Registers a page type as a modal route.
    /// Modals bypass Shell routing — they are resolved from DI and pushed via PushModalAsync.
    /// Call this from AppShell alongside Routing.RegisterRoute for push routes.
    /// </summary>
    public void RegisterModal(string route, Type pageType)
        => _modalRegistry[route] = pageType;

    public async Task GoToAsync(string route, bool animated = true)
    {
        if (!await CheckNavigationGuardAsync())
            return;

        await Shell.Current.GoToAsync(route, animated);
    }

    public async Task GoToAsync<TParams>(string route, TParams parameters, bool animated = true) where TParams : class
    {
        if (!await CheckNavigationGuardAsync())
            return;

        var shellParams = new ShellNavigationQueryParameters { ["__params"] = parameters };
        await Shell.Current.GoToAsync(route, animated, shellParams);
    }

    /// <summary>
    /// Navigates back and injects __isBack=true so the parent ViewModel can
    /// skip re-fetching data in InitializeFromQueryAsync.
    /// </summary>
    public async Task GoBackAsync(bool animated = true)
    {
        if (!await CheckNavigationGuardAsync())
            return;

        var shellParams = new ShellNavigationQueryParameters { ["__isBack"] = true };
        await Shell.Current.GoToAsync("..", animated, shellParams);
    }

    /// <summary>
    /// Navigates back multiple levels using repeated ".." segments.
    /// Example: levels=2 produces "../.." which pops 2 pages.
    /// </summary>
    public async Task GoBackAsync(int levels, bool animated = true)
    {
        if (levels < 1)
            throw new ArgumentOutOfRangeException(nameof(levels), "Levels must be at least 1");

        if (!await CheckNavigationGuardAsync())
            return;

        var route = string.Join("/", Enumerable.Repeat("..", levels));
        var shellParams = new ShellNavigationQueryParameters { ["__isBack"] = true };
        await Shell.Current.GoToAsync(route, animated, shellParams);
    }

    /// <summary>
    /// Navigates to the root of the current tab by using the absolute route.
    /// This clears the navigation stack within the current tab.
    /// </summary>
    public async Task GoBackToRootAsync(bool animated = true)
    {
        if (!await CheckNavigationGuardAsync())
            return;

        // Get the current shell section (tab) route
        var currentRoute = Shell.Current.CurrentState.Location.OriginalString;

        // Extract the root route (first segment after //)
        // Format is typically "//TabRoute/Page1/Page2"
        var segments = currentRoute.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length > 0)
        {
            var rootRoute = $"//{segments[0]}";
            var shellParams = new ShellNavigationQueryParameters { ["__isBack"] = true };
            await Shell.Current.GoToAsync(rootRoute, animated, shellParams);
        }
    }

    public Task PresentModalAsync(string route, bool animated = true)
    {
        var page = ResolveModalPage(route);
        return Shell.Current.Navigation.PushModalAsync(page, animated);
    }

    /// <summary>
    /// Presents a modal page and delivers parameters directly via INavigationParameterReceiver.
    /// Shell routing is bypassed for modals (PushModalAsync), so ShellNavigationQueryParameters
    /// cannot carry params — instead we call ApplyNavigationParameters before pushing.
    /// </summary>
    public Task PresentModalAsync<TParams>(string route, TParams parameters, bool animated = true) where TParams : class
    {
        var page = ResolveModalPage(route);
        if (page.BindingContext is INavigationParameterReceiver receiver)
            receiver.ApplyNavigationParameters(new Dictionary<string, object> { ["__params"] = parameters });
        return Shell.Current.Navigation.PushModalAsync(page, animated);
    }

    public Task DismissModalAsync(bool animated = true)
        => Shell.Current.Navigation.PopModalAsync(animated);

    /// <summary>
    /// Presents a modal and awaits a result. The modal ViewModel must implement
    /// IModalResultProvider&lt;TResult&gt; and call CompleteWithResult or CompleteAsCancelled.
    /// </summary>
    public async Task<NavigationResult<TResult>> PresentModalForResultAsync<TResult>(string route, bool animated = true)
    {
        var page = ResolveModalPage(route);

        if (page.BindingContext is not IModalResultProvider<TResult> resultProvider)
            throw new InvalidOperationException(
                $"ViewModel for route '{route}' does not implement IModalResultProvider<{typeof(TResult).Name}>. " +
                "Use PresentModalAsync instead, or implement the interface.");

        var tcs = new TaskCompletionSource<NavigationResult<TResult>>();
        resultProvider.SetResultCompletion(tcs);

        await Shell.Current.Navigation.PushModalAsync(page, animated);

        return await tcs.Task;
    }

    /// <summary>
    /// Presents a modal with parameters and awaits a result.
    /// </summary>
    public async Task<NavigationResult<TResult>> PresentModalForResultAsync<TParams, TResult>(
        string route,
        TParams parameters,
        bool animated = true) where TParams : class
    {
        var page = ResolveModalPage(route);

        if (page.BindingContext is not IModalResultProvider<TResult> resultProvider)
            throw new InvalidOperationException(
                $"ViewModel for route '{route}' does not implement IModalResultProvider<{typeof(TResult).Name}>. " +
                "Use PresentModalAsync instead, or implement the interface.");

        // Deliver parameters before setting up result completion
        if (page.BindingContext is INavigationParameterReceiver receiver)
            receiver.ApplyNavigationParameters(new Dictionary<string, object> { ["__params"] = parameters });

        var tcs = new TaskCompletionSource<NavigationResult<TResult>>();
        resultProvider.SetResultCompletion(tcs);

        await Shell.Current.Navigation.PushModalAsync(page, animated);

        return await tcs.Task;
    }

    /// <summary>
    /// Switches to a different tab using absolute navigation.
    /// The "//" prefix tells Shell to navigate from the root.
    /// </summary>
    public Task SwitchTabAsync(string tabRoute, bool animated = true)
        => Shell.Current.GoToAsync($"//{tabRoute}", animated);

    /// <summary>
    /// Switches to a tab and navigates to a page within that tab in one operation.
    /// Uses the pattern "//TabRoute/PageRoute" with parameters.
    /// </summary>
    public Task SwitchTabAndNavigateAsync<TParams>(
        string tabRoute,
        string pageRoute,
        TParams parameters,
        bool animated = true) where TParams : class
    {
        var shellParams = new ShellNavigationQueryParameters { ["__params"] = parameters };
        return Shell.Current.GoToAsync($"//{tabRoute}/{pageRoute}", animated, shellParams);
    }

    private ContentPage ResolveModalPage(string route)
    {
        if (!_modalRegistry.TryGetValue(route, out var pageType))
            throw new InvalidOperationException(
                $"No modal registered for route '{route}'. " +
                $"Call navigationService.RegisterModal(\"{route}\", typeof(YourPage)) from AppShell.");

        return (ContentPage)serviceProvider.GetRequiredService(pageType);
    }

    /// <summary>
    /// Checks if the current page's ViewModel implements INavigationGuard and allows leaving.
    /// Returns true if navigation should proceed, false if blocked.
    /// </summary>
    private async Task<bool> CheckNavigationGuardAsync()
    {
        var currentPage = Shell.Current?.CurrentPage;
        if (currentPage?.BindingContext is INavigationGuard guard)
        {
            if (guard.HasUnsavedChanges)
            {
                return await guard.CanLeaveAsync();
            }
        }
        return true;
    }
}
