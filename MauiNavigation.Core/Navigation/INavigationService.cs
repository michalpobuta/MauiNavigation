namespace MauiNavigation.Core.Navigation;

public interface INavigationService
{
    /// <summary>
    /// Registers a page type as a modal route. Call once from AppShell.
    /// Modals bypass Shell routing so they cannot use Routing.RegisterRoute.
    /// </summary>
    void RegisterModal(string route, Type pageType);

    Task GoToAsync(string route, bool animated = true);
    Task GoToAsync<TParams>(string route, TParams parameters, bool animated = true) where TParams : class;
    Task GoBackAsync(bool animated = true);

    /// <summary>
    /// Navigates back multiple levels at once.
    /// Use when you need to pop several pages from the stack (e.g., after a multi-step flow).
    /// </summary>
    /// <param name="levels">Number of levels to go back (1 = same as GoBackAsync)</param>
    Task GoBackAsync(int levels, bool animated = true);

    /// <summary>
    /// Navigates back to the root of the current tab, clearing the navigation stack.
    /// Use after completing a multi-step flow to return to the tab's main page.
    /// </summary>
    Task GoBackToRootAsync(bool animated = true);

    Task PresentModalAsync(string route, bool animated = true);
    Task PresentModalAsync<TParams>(string route, TParams parameters, bool animated = true) where TParams : class;
    Task DismissModalAsync(bool animated = true);

    /// <summary>
    /// Presents a modal and awaits a result of type TResult.
    /// The modal ViewModel must implement IModalResultProvider&lt;TResult&gt;.
    /// Returns NavigationResult&lt;TResult&gt; — check Succeeded to see if user confirmed or cancelled.
    /// </summary>
    Task<NavigationResult<TResult>> PresentModalForResultAsync<TResult>(string route, bool animated = true);

    /// <summary>
    /// Presents a modal with parameters and awaits a result.
    /// The modal ViewModel must implement IModalResultProvider&lt;TResult&gt;.
    /// </summary>
    Task<NavigationResult<TResult>> PresentModalForResultAsync<TParams, TResult>(string route, TParams parameters, bool animated = true)
        where TParams : class;

    /// <summary>
    /// Switches to a different tab using absolute navigation (//route).
    /// Use when you need to change tabs programmatically.
    /// </summary>
    /// <param name="tabRoute">The route of the tab's ShellContent (e.g., Routes.BrowseTab)</param>
    Task SwitchTabAsync(string tabRoute, bool animated = true);

    /// <summary>
    /// Switches to a tab and then navigates to a page within that tab.
    /// Combines tab switch and page push in a single navigation.
    /// Example: SwitchTabAndNavigateAsync(Routes.BrowseTab, Routes.MovieDetail, movieParams)
    /// </summary>
    Task SwitchTabAndNavigateAsync<TParams>(string tabRoute, string pageRoute, TParams parameters, bool animated = true)
        where TParams : class;
}
