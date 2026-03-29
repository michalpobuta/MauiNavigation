namespace MauiNavigation.Core.Navigation;

/// <summary>
/// Interface for ViewModels that need to confirm before navigation away.
/// Implement CanLeaveAsync to show a confirmation dialog when the user
/// has unsaved changes or is in the middle of a multi-step process.
/// </summary>
public interface INavigationGuard
{
    /// <summary>
    /// Called before navigating away from this page.
    /// Return true to allow navigation, false to block it.
    /// Use to show "Discard changes?" confirmation dialogs.
    /// </summary>
    /// <remarks>
    /// This is only called for programmatic navigation (GoBackAsync, GoToAsync).
    /// Hardware/gesture back buttons bypass this — use platform-specific
    /// back button handling for complete coverage.
    /// </remarks>
    Task<bool> CanLeaveAsync();

    /// <summary>
    /// Returns true if the ViewModel has unsaved changes that would be lost on navigation.
    /// Use this to determine whether to show a confirmation dialog.
    /// </summary>
    bool HasUnsavedChanges { get; }
}
