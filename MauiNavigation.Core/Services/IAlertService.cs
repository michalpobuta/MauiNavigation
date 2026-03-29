namespace MauiNavigation.Core.Services;

/// <summary>
/// Abstraction for user-facing alerts and notifications.
/// Implemented in the App project using MAUI's DisplayAlert and CommunityToolkit toasts.
/// </summary>
public interface IAlertService
{
    /// <summary>
    /// Shows a simple informational alert with an OK button.
    /// </summary>
    Task ShowAlertAsync(string title, string message, string cancel = "OK");

    /// <summary>
    /// Shows a confirmation dialog with accept/cancel buttons.
    /// Returns true if the user tapped accept, false otherwise.
    /// </summary>
    Task<bool> ShowConfirmAsync(string title, string message, string accept = "Yes", string cancel = "No");

    /// <summary>
    /// Shows a brief toast notification that auto-dismisses.
    /// Use for non-critical feedback (e.g., "Saved", "Copied to clipboard").
    /// </summary>
    Task ShowToastAsync(string message, ToastDuration duration = ToastDuration.Short);

    /// <summary>
    /// Shows an error alert. Use for recoverable errors where the user should be informed.
    /// </summary>
    Task ShowErrorAsync(string message, string title = "Error");
}

public enum ToastDuration
{
    Short,
    Long
}
