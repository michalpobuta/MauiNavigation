using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;
using MauiNavigation.Core.Services;
using ToastDuration = MauiNavigation.Core.Services.ToastDuration;

namespace MauiNavigation.Infrastructure;

/// <summary>
/// MAUI implementation of IAlertService using DisplayAlertAsync and CommunityToolkit toasts.
/// Registered as Singleton — stateless, safe to share.
/// </summary>
public class AlertService : IAlertService
{
    public async Task ShowAlertAsync(string title, string message, string cancel = "OK")
    {
        var page = GetCurrentPage();
        if (page is not null)
            await page.DisplayAlertAsync(title, message, cancel);
    }

    public async Task<bool> ShowConfirmAsync(string title, string message, string accept = "Yes", string cancel = "No")
    {
        var page = GetCurrentPage();
        if (page is null)
            return false;

        return await page.DisplayAlertAsync(title, message, accept, cancel);
    }

    public async Task ShowToastAsync(string message, ToastDuration duration = ToastDuration.Short)
    {
        var mauiDuration = duration == ToastDuration.Short
            ? CommunityToolkit.Maui.Core.ToastDuration.Short
            : CommunityToolkit.Maui.Core.ToastDuration.Long;

        var toast = Toast.Make(message, mauiDuration);
        await toast.Show();
    }

    public Task ShowErrorAsync(string message, string title = "Error")
        => ShowAlertAsync(title, message, "OK");

    private static Page? GetCurrentPage()
    {
        // Shell.Current.CurrentPage is the most reliable way to get the visible page
        if (Shell.Current?.CurrentPage is not null)
            return Shell.Current.CurrentPage;

        // Fallback for modal scenarios
        return Application.Current?.Windows.FirstOrDefault()?.Page;
    }
}
