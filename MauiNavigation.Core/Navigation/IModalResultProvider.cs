namespace MauiNavigation.Core.Navigation;

/// <summary>
/// Interface for ViewModels that can return a result when their modal is dismissed.
/// Implement this in a ViewModel to enable result-returning modal navigation.
/// </summary>
public interface IModalResultProvider<TResult>
{
    /// <summary>
    /// Sets the TaskCompletionSource that will receive the modal result.
    /// Called automatically by ShellNavigationService.PresentModalForResultAsync.
    /// </summary>
    void SetResultCompletion(TaskCompletionSource<NavigationResult<TResult>> tcs);

    /// <summary>
    /// Call from your Apply/Save command to complete the modal with a successful result.
    /// </summary>
    void CompleteWithResult(TResult result);

    /// <summary>
    /// Call from your Cancel/Dismiss command to complete the modal as cancelled.
    /// </summary>
    void CompleteAsCancelled();
}
