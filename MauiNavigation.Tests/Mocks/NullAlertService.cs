using MauiNavigation.Core.Services;

namespace MauiNavigation.Tests.Mocks;

/// <summary>
/// Test implementation of IAlertService that does nothing.
/// Use when you need to provide an IAlertService but don't need to verify interactions.
/// </summary>
public class NullAlertService : IAlertService
{
    public static readonly NullAlertService Instance = new();

    public Task ShowAlertAsync(string title, string message, string cancel = "OK")
        => Task.CompletedTask;

    public Task<bool> ShowConfirmAsync(string title, string message, string accept = "Yes", string cancel = "No")
        => Task.FromResult(true);

    public Task ShowToastAsync(string message, ToastDuration duration = ToastDuration.Short)
        => Task.CompletedTask;

    public Task ShowErrorAsync(string message, string title = "Error")
        => Task.CompletedTask;
}
