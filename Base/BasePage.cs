using Microsoft.Maui.Controls;
using MauiNavigation.Core.Base;
using MauiNavigation.Core.Navigation;

namespace MauiNavigation.Base;

/// <summary>
/// Base page for all content pages in this app.
/// - Wires OnAppearing/OnDisappearing to ViewModel lifecycle methods.
/// - Implements IQueryAttributable and forwards to ViewModel.ApplyNavigationParameters,
///   bridging MAUI's Shell routing to the Core INavigationParameterReceiver interface.
/// - Disposes the ViewModel on Dispose.
/// </summary>
public abstract class BasePage<TViewModel> : ContentPage, IQueryAttributable, IDisposable
    where TViewModel : BaseViewModel<TViewModel>
{
    protected BasePage(TViewModel viewModel, bool showNavBar = false)
    {
        BindingContext = viewModel;
        Shell.SetNavBarIsVisible(this, showNavBar);
    }

    protected TViewModel ViewModel => (TViewModel)BindingContext;

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _ = ViewModel.OnAppearingInternal();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        // NOTE: fire-and-forget — MAUI's OnDisappearing cannot be async.
        // Subclasses must not rely on OnDisappearingInternal completing before
        // the next page's OnAppearing fires.
        _ = ViewModel.OnDisappearingInternal();
    }

    /// <summary>
    /// Shell calls this when navigating to this page (push or query string).
    /// Forwards to ViewModel.ApplyNavigationParameters — the only place in
    /// the app that touches MAUI's IQueryAttributable.
    /// NOTE: This is NOT called when a modal above this page is dismissed.
    /// Handle post-dismiss refresh in OnAppearingAsync instead.
    /// </summary>
    public void ApplyQueryAttributes(IDictionary<string, object> query)
        => ViewModel.ApplyNavigationParameters(query);

    // NOTE: MAUI does not call Dispose() on pages automatically.
    // CTS cancellation is guaranteed via OnDisappearing, not here.
    // Dispose() exists as a belt-and-suspenders for explicit cleanup in tests or custom teardown.
    public void Dispose()
    {
        if (BindingContext is IDisposable disposable)
            disposable.Dispose();
        GC.SuppressFinalize(this);
    }
}
