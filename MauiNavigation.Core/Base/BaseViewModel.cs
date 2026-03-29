using CommunityToolkit.Mvvm.ComponentModel;
using MauiNavigation.Core.Navigation;
using Microsoft.Extensions.Logging;

namespace MauiNavigation.Core.Base;

/// <summary>
/// Base class for all ViewModels. Provides:
/// <list type="bullet">
///   <item>Page lifecycle hooks (<see cref="OnAppearingAsync"/> / <see cref="OnDisappearingAsync"/>)</item>
///   <item>Lifecycle-scoped <see cref="CancellationToken"/> automatically cancelled on disappear/dispose</item>
///   <item>Ref-counted <see cref="IsBusy"/> flag (nested operations don't flip it off prematurely)</item>
///   <item>Navigation parameter delivery via <see cref="ApplyNavigationParameters"/></item>
/// </list>
/// </summary>
public abstract partial class BaseViewModel<T> : ObservableValidator, INavigationParameterReceiver, IDisposable
    where T : BaseViewModel<T>
{
    private readonly Lock _lifecycleLock = new();
    private CancellationTokenSource? _lifecycleCts;
    private int _busyCount;
    private bool _isDisposed;

    protected BaseViewModel(BaseViewModelFacade facade)
    {
        Facade = facade;
    }

    protected BaseViewModelFacade Facade { get; }

    [ObservableProperty]
    private bool _isBusy;

    /// <summary>
    /// The current lifecycle cancellation token. Created lazily; re-created each time
    /// the page re-appears after a disappearing cycle.
    /// </summary>
    protected CancellationToken LifecycleToken
    {
        get
        {
            lock (_lifecycleLock)
            {
                _lifecycleCts ??= new CancellationTokenSource();
                return _lifecycleCts.Token;
            }
        }
    }

    /// <summary>
    /// Called by BasePage.OnAppearing. Do not call from subclasses.
    /// Manages CTS lifecycle then delegates to OnAppearingAsync.
    /// </summary>
    public async Task OnAppearingInternal()
    {
        CancellationToken token;

        lock (_lifecycleLock)
        {
            if (_isDisposed) return;

            // Re-use existing CTS if still valid (e.g. created by ApplyNavigationParameters
            // before OnAppearing fires). Only replace if cancelled from a previous cycle.
            if (_lifecycleCts is null || _lifecycleCts.IsCancellationRequested)
            {
                _lifecycleCts?.Dispose();
                _lifecycleCts = new CancellationTokenSource();
            }

            token = _lifecycleCts.Token;
        }

        try
        {
            await OnAppearingAsync(token);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            Facade.Logger.LogError(ex, "Error in OnAppearingAsync for {ViewModel}", typeof(T).Name);
        }
    }

    /// <summary>
    /// Called by BasePage.OnDisappearing. Do not call from subclasses.
    /// Cancels the lifecycle CTS then delegates to OnDisappearingAsync.
    /// </summary>
    public async Task OnDisappearingInternal()
    {
        CancellationTokenSource? cts;

        lock (_lifecycleLock)
        {
            cts = _lifecycleCts;
        }

        // Cancel outside the lock — registered callbacks could be heavy.
        if (cts is not null)
            await cts.CancelAsync();

        try
        {
            await OnDisappearingAsync();
        }
        catch (Exception ex)
        {
            Facade.Logger.LogError(ex, "Error in OnDisappearingAsync for {ViewModel}", typeof(T).Name);
        }
        finally
        {
            lock (_lifecycleLock)
            {
                // Guard against rapid re-appear: only dispose if the field still
                // points to the same CTS we captured above.
                if (ReferenceEquals(_lifecycleCts, cts))
                {
                    _lifecycleCts?.Dispose();
                    _lifecycleCts = null;
                }
            }
        }
    }

    /// <summary>Override to run logic when the page appears. Cancellation is managed by the base.</summary>
    protected virtual Task OnAppearingAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>Override to run cleanup when the page disappears.</summary>
    protected virtual Task OnDisappearingAsync() => Task.CompletedTask;

    /// <summary>Override to receive navigation parameters. isGoBack is true when returning from a child page.</summary>
    public virtual Task InitializeFromQueryAsync(IDictionary<string, object> query, bool isGoBack, CancellationToken ct) => Task.CompletedTask;

    /// <summary>
    /// Implements INavigationParameterReceiver. Called by BasePage.ApplyQueryAttributes.
    /// Extracts the __isBack flag, then fire-and-forgets InitializeFromQueryAsync with IsBusy=true.
    /// </summary>
    public void ApplyNavigationParameters(IDictionary<string, object> query)
    {
        // ShellNavigationQueryParameters is read-only — use ContainsKey, not Remove.
        // Key presence is the sentinel; the stored value is irrelevant.
        var isGoBack = query.ContainsKey("__isBack");
        SafeFireAndForget(ct => InitializeFromQueryAsync(query, isGoBack, ct), showLoader: true);
    }

    /// <summary>
    /// Fire-and-forget wrapper. Catches all exceptions and logs them.
    /// OperationCanceledException is silently swallowed.
    /// </summary>
    /// <param name="action">The async action to execute.</param>
    /// <param name="showLoader">If true, sets IsBusy while running.</param>
    /// <param name="onError">Optional error handler. If null, errors are logged only. 
    /// If provided, called with the exception — use for user-facing error messages.</param>
    protected void SafeFireAndForget(
        Func<CancellationToken, Task> action,
        bool showLoader = false,
        Func<Exception, Task>? onError = null)
        => _ = ExecuteSafeAsync(action, showLoader, onError);

    /// <summary>
    /// Fire-and-forget that shows error to user via alert service.
    /// Use when errors should be surfaced to the user.
    /// </summary>
    protected void SafeFireAndForgetWithErrorAlert(
        Func<CancellationToken, Task> action,
        bool showLoader = false,
        string errorTitle = "Error")
        => _ = ExecuteSafeAsync(action, showLoader, ex => Facade.Alerts.ShowErrorAsync(ex.Message, errorTitle));

    /// <summary>
    /// Cancels the lifecycle CancellationToken and releases resources.
    /// Thread-safe and idempotent — safe to call multiple times.
    /// </summary>
    public virtual void Dispose()
    {
        lock (_lifecycleLock)
        {
            if (_isDisposed) return;
            _isDisposed = true;
            _lifecycleCts?.Cancel();
            _lifecycleCts?.Dispose();
            _lifecycleCts = null;
        }

        GC.SuppressFinalize(this);
    }

    private async Task ExecuteSafeAsync(
        Func<CancellationToken, Task> action,
        bool showLoader,
        Func<Exception, Task>? onError = null)
    {
        if (showLoader)
        {
            Interlocked.Increment(ref _busyCount);
            IsBusy = true;
        }

        try
        {
            await action(LifecycleToken);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            Facade.Logger.LogError(ex, "Unhandled error in {ViewModel}", typeof(T).Name);

            if (onError is not null)
            {
                try
                {
                    await onError(ex);
                }
                catch (Exception alertEx)
                {
                    Facade.Logger.LogError(alertEx, "Error in onError callback for {ViewModel}", typeof(T).Name);
                }
            }
        }
        finally
        {
            // Decrement is in finally — always runs even if action throws.
            // Ref-counting means nested SafeFireAndForget calls don't flip IsBusy off prematurely.
            if (showLoader && Interlocked.Decrement(ref _busyCount) <= 0)
                IsBusy = false;
        }
    }
}
