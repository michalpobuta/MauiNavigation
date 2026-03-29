namespace MauiNavigation.Core.Navigation;

/// <summary>
/// Implemented by ViewModels to receive navigation parameters.
/// Called by <c>BasePage.ApplyQueryAttributes</c> (for Shell push/back) and directly by
/// <c>ShellNavigationService.PresentModalAsync</c> (for modals, which bypass Shell routing).
/// <para>
/// The reserved key <c>"__isBack"</c> signals a back navigation — its <em>presence</em> (not value)
/// is the sentinel. Do not store any value under this key for forward navigations.
/// </para>
/// </summary>
public interface INavigationParameterReceiver
{
    void ApplyNavigationParameters(IDictionary<string, object> query);
}
