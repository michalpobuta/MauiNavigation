# MAUI Navigation Guide: Shell Navigation with a Framework-Agnostic Core

This article walks through the navigation architecture used in the Movies example app in this repository. It targets intermediate developers who want a complete mental model, with senior-level callouts explaining why specific decisions were made.

The full source is in this repo. Read the code alongside the article — sections reference file paths directly.

---

## Table of Contents

### Fundamentals
1. [Why Shell Navigation + the Core/App Split](#1-why-shell-navigation--the-coreapp-split)
2. [Project Setup](#2-project-setup)
3. [INavigationService Contract + Strongly-Typed Parameters](#3-inavigationservice-contract--strongly-typed-parameters)
4. [BaseViewModelFacade](#4-baseviewmodelfacade)
5. [BaseViewModel\<T\>](#5-baseviewmodelt)
6. [BasePage\<TViewModel\>](#6-basepagetviewmodel)
7. [AppShell: TabBar + Route Registration](#7-appshell-tabbar--route-registration)
8. [ShellNavigationService](#8-shellnavigationservice)
9. [DI Wiring in MauiProgram](#9-di-wiring-in-mauiprogram)
10. [Real-World Walk-Through](#10-real-world-walk-through)
11. [Gotchas and Tips](#11-gotchas-and-tips)

### Advanced Patterns
12. [Error Handling and User Feedback](#12-error-handling-and-user-feedback)
13. [Returning Results from Modals](#13-returning-results-from-modals)
14. [Tab Navigation](#14-tab-navigation)
15. [Advanced Back Navigation](#15-advanced-back-navigation)
16. [Navigation Guards](#16-navigation-guards)
17. [Deep Linking](#17-deep-linking)
18. [Shared State Between Pages](#18-shared-state-between-pages)
19. [Testing ViewModels](#19-testing-viewmodels)
20. [Quick Reference](#20-quick-reference)

---

## 1. Why Shell Navigation + the Core/App Split

### MAUI Shell navigation

MAUI Shell provides URI-based navigation: you call `Shell.Current.GoToAsync("MovieDetailPage")` and Shell handles pushing a page onto the navigation stack, managing the back button, and delivering query parameters via `IQueryAttributable`. You register routes with `Routing.RegisterRoute` once at startup and navigate to them by name from anywhere in the app.

This is a significant improvement over manually managing a navigation stack. Shell gives you:

- A back stack that works correctly across tabs
- Query parameter delivery to destination pages
- Deep link support for free (the same URIs work as app deep links)
- A `..` route that always means "go back one level"

### The Core/App split

The app is structured as two projects:

| Project | Target | Purpose |
|---|---|---|
| `MauiNavigation.Core` | `net10.0` | ViewModels, navigation contracts, parameters, models |
| `MauiNavigation` (the App) | `net10.0-android`, `net10.0-ios`, etc. | Pages, infrastructure, DI wiring |

`MauiNavigation.Core` has **zero** `Microsoft.Maui.Controls` references. This is enforced at compile time by the csproj — if a ViewModel accidentally tries to use `Shell.Current` or `ContentPage`, it won't compile. The consequence is real: you can run the entire ViewModel test suite in a plain `net10.0` xUnit project with no MAUI infrastructure, no emulator, no device.

> **Senior tip:** The boundary also makes the architecture explicit to new team members. If a developer asks "can I use `Shell.Current` in a ViewModel?", the compiler already answered: no. You don't need a code review comment.

The split does require solving one problem: ViewModels need to trigger navigation, but they cannot reference MAUI page types. The `INavigationService` interface and `Routes` constants exist to solve exactly this problem — covered in Section 3.

---

## 2. Project Setup

### Core project (`MauiNavigation.Core/MauiNavigation.Core.csproj`)

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <RootNamespace>MauiNavigation.Core</RootNamespace>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="CommunityToolkit.Mvvm" Version="8.4.0" />
    <PackageReference Include="Microsoft.Extensions.Logging.Abstractions" Version="10.0.0" />
  </ItemGroup>
</Project>
```

Two dependencies only:

- **CommunityToolkit.Mvvm** — `ObservableObject`, `ObservableValidator`, `[ObservableProperty]`, `[RelayCommand]`. The source-generator-based MVVM toolkit is platform-agnostic.
- **Microsoft.Extensions.Logging.Abstractions** — the `ILogger` interface only. No concrete logger is pulled in; the App project supplies that at runtime.

There is no `Microsoft.Maui.Controls` reference here. That absence is the enforcement mechanism.

### App project (`MauiNavigation.csproj`)

```xml
<Project Sdk="Microsoft.NET.Sdk">
    <PropertyGroup>
        <TargetFrameworks>net10.0-android</TargetFrameworks>
        <TargetFrameworks Condition="!$([MSBuild]::IsOSPlatform('linux'))">$(TargetFrameworks);net10.0-ios;net10.0-maccatalyst</TargetFrameworks>
        <TargetFrameworks Condition="$([MSBuild]::IsOSPlatform('windows'))">$(TargetFrameworks);net10.0-windows10.0.19041.0</TargetFrameworks>
        <OutputType>Exe</OutputType>
        <RootNamespace>MauiNavigation</RootNamespace>
        <UseMaui>true</UseMaui>
        ...
    </PropertyGroup>
    <ItemGroup>
        <PackageReference Include="Microsoft.Maui.Controls" Version="$(MauiVersion)" />
        <PackageReference Include="Microsoft.Extensions.Logging.Debug" Version="10.0.0" />
        <PackageReference Include="CommunityToolkit.Maui" Version="9.0.3" />
    </ItemGroup>
    <ItemGroup>
      <ProjectReference Include="MauiNavigation.Core\MauiNavigation.Core.csproj" />
    </ItemGroup>
</Project>
```

The App project references Core via `<ProjectReference>` and adds:

- **Microsoft.Maui.Controls** — the MAUI framework itself (implicit in the `UseMaui` SDK, made explicit here)
- **CommunityToolkit.Maui** — used for `UseMauiCommunityToolkit()` in `MauiProgram`
- **Microsoft.Extensions.Logging.Debug** — the concrete debug logger registered only in `#if DEBUG`

---

## 3. INavigationService Contract + Strongly-Typed Parameters

### The interface

```csharp
// MauiNavigation.Core/Navigation/INavigationService.cs
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
    Task PresentModalAsync(string route, bool animated = true);
    Task PresentModalAsync<TParams>(string route, TParams parameters, bool animated = true) where TParams : class;
    Task DismissModalAsync(bool animated = true);
}
```

This interface lives in Core so ViewModels can call it. The only implementation (`ShellNavigationService`) lives in the App project and references `Shell.Current` directly.

### Routes

```csharp
// MauiNavigation.Core/Navigation/Routes.cs
namespace MauiNavigation.Core.Navigation;

public static class Routes
{
    public const string MovieDetail = "MovieDetailPage";
    public const string Filter = "FilterPage";
}
```

Why does Core need a `Routes` class at all? Because ViewModels need to navigate:

```csharp
Facade.Navigation.GoToAsync(Routes.MovieDetail, new MovieDetailParameters(movie.Id, movie.Title));
```

Core cannot write `nameof(MovieDetailPage)` — `MovieDetailPage` is a MAUI page type that lives in the App project, which Core cannot reference. The `Routes` class bridges this: it holds the same string values that `AppShell.xaml.cs` uses in `Routing.RegisterRoute(nameof(MovieDetailPage), ...)`.

> **Senior tip:** The constraint is that `Routes.MovieDetail` must equal `nameof(MovieDetailPage)` at all times. Both are currently the string `"MovieDetailPage"`. The only way this gets out of sync is if someone renames the page class without updating the constant. A test that resolves the route against the DI container (or a simple string comparison test) catches this before it reaches production.

### Strongly-typed parameters

```csharp
// MauiNavigation.Core/Navigation/Parameters/MovieDetailParameters.cs
namespace MauiNavigation.Core.Navigation.Parameters;

public record MovieDetailParameters(int MovieId, string Title);
```

```csharp
// MauiNavigation.Core/Navigation/Parameters/FilterParameters.cs
namespace MauiNavigation.Core.Navigation.Parameters;

public record FilterParameters(string? Genre, int? MinYear);
```

Parameters are plain C# records. They have no dependency on MAUI or on any serialization framework. Using records gives structural equality for free, which is useful in tests.

### The `__params` key pattern

When `GoToAsync<TParams>` is called, the implementation wraps the parameter object in a `ShellNavigationQueryParameters` dictionary under the key `"__params"`:

```csharp
var shellParams = new ShellNavigationQueryParameters { ["__params"] = parameters };
Shell.Current.GoToAsync(route, animated, shellParams);
```

`ShellNavigationQueryParameters` is a MAUI type that passes objects by reference through Shell routing rather than serializing them to URL query strings. The destination ViewModel extracts the object by checking for `"__params"`:

```csharp
if (query.TryGetValue("__params", out var p) && p is MovieDetailParameters parms)
{
    MovieId = parms.MovieId;
    Title = parms.Title;
}
```

This avoids serialization entirely. The parameter object is the same instance the calling ViewModel created.

---

## 4. BaseViewModelFacade

```csharp
// MauiNavigation.Core/Base/BaseViewModelFacade.cs
using MauiNavigation.Core.Navigation;
using Microsoft.Extensions.Logging;

namespace MauiNavigation.Core.Base;

/// <summary>
/// Aggregates shared services so ViewModels have a single constructor dependency.
/// Registered as Singleton in DI.
/// </summary>
public class BaseViewModelFacade(INavigationService navigation, ILogger<BaseViewModelFacade> logger)
{
    public INavigationService Navigation { get; } = navigation;
    public ILogger Logger { get; } = logger;
}
```

Every ViewModel in the app needs at minimum `INavigationService` and `ILogger`. Without the facade, every ViewModel constructor would declare both. When you add a third shared service (say, an analytics service), you update the facade's constructor and nothing else changes — no ViewModel constructor is modified, no DI registration for every ViewModel is touched.

The pattern is called the **Facade** pattern: a single aggregating object that reduces the number of constructor parameters for consumers.

> **Senior tip:** `BaseViewModelFacade` is registered as a **Singleton**, while ViewModels are **Transient**. This is intentional and correct. Multiple transient ViewModel instances share a single facade instance. The facade holds no per-ViewModel state — it is a container of stateless service references. If the facade held state, the singleton lifetime would be wrong. Always verify that services aggregated in a facade are themselves safe to share across transient consumers (i.e., they are stateless or thread-safe).

---

## 5. BaseViewModel\<T\>

```csharp
// MauiNavigation.Core/Base/BaseViewModel.cs
using CommunityToolkit.Mvvm.ComponentModel;
using MauiNavigation.Core.Navigation;
using Microsoft.Extensions.Logging;

namespace MauiNavigation.Core.Base;

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

    public async Task OnAppearingInternal()
    {
        CancellationToken token;

        lock (_lifecycleLock)
        {
            if (_isDisposed) return;

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

    public async Task OnDisappearingInternal()
    {
        CancellationTokenSource? cts;

        lock (_lifecycleLock)
        {
            cts = _lifecycleCts;
        }

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
                if (ReferenceEquals(_lifecycleCts, cts))
                {
                    _lifecycleCts?.Dispose();
                    _lifecycleCts = null;
                }
            }
        }
    }

    protected virtual Task OnAppearingAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    protected virtual Task OnDisappearingAsync() => Task.CompletedTask;

    public virtual Task InitializeFromQueryAsync(IDictionary<string, object> query, bool isGoBack, CancellationToken ct) => Task.CompletedTask;

    public void ApplyNavigationParameters(IDictionary<string, object> query)
    {
        // ShellNavigationQueryParameters is read-only — use ContainsKey, not Remove.
        var isGoBack = query.ContainsKey("__isBack");
        SafeFireAndForget(ct => InitializeFromQueryAsync(query, isGoBack, ct), showLoader: true);
    }

    protected void SafeFireAndForget(Func<CancellationToken, Task> action, bool showLoader = false)
        => _ = ExecuteSafeAsync(action, showLoader);

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

    private async Task ExecuteSafeAsync(Func<CancellationToken, Task> action, bool showLoader)
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
        }
        finally
        {
            if (showLoader && Interlocked.Decrement(ref _busyCount) <= 0)
                IsBusy = false;
        }
    }
}
```

There are five concepts worth unpacking.

### Lifecycle wrappers vs virtual hooks

`BasePage` calls `OnAppearingInternal()` (public, sealed in practice) and `OnDisappearingInternal()`. These are the *wrappers* — they manage the `CancellationTokenSource` and error handling. Subclasses override the *virtual hooks* `OnAppearingAsync(ct)` and `OnDisappearingAsync()`.

The call chain for appearing is:

```
BasePage.OnAppearing()
  → ViewModel.OnAppearingInternal()        [manages CTS, handles exceptions]
    → virtual OnAppearingAsync(token)      [override in your ViewModel]
```

This structure means a subclass can never accidentally skip the CTS setup or swallow exceptions from the wrapper layer — they only override the payload method.

### CancellationToken lifecycle

The `LifecycleToken` is created lazily when first accessed and cancelled in `OnDisappearingInternal`. On re-appear, `OnAppearingInternal` checks whether the existing CTS is cancelled and creates a fresh one if so — but if the CTS already exists and is still valid (e.g. it was created by `ApplyNavigationParameters` before `OnAppearing` fired), it is reused rather than replaced.

Shell calls `ApplyQueryAttributes` — and therefore `ApplyNavigationParameters` — before `OnAppearing`. This ordering means async work started by `InitializeFromQueryAsync` may already be in-flight when `OnAppearingAsync` is called.

This means any async work started in `OnAppearingAsync` (or via `SafeFireAndForget`) will be cancelled automatically when the page disappears — without the ViewModel needing to track the token manually.

> **Senior tip:** The `ReferenceEquals` guard in the `finally` block of `OnDisappearingInternal` exists to handle a specific race condition. If a page disappears and re-appears very quickly — faster than the disappear task completes — a new CTS will have been created by `OnAppearingInternal`. Without the `ReferenceEquals` check, the `finally` block would dispose the *new* CTS, cancelling work that was legitimately started for the new appear cycle. The guard ensures only the CTS that was captured at the start of the disappearing cycle is disposed.

### ApplyNavigationParameters and `isGoBack`

```csharp
public void ApplyNavigationParameters(IDictionary<string, object> query)
{
    // ShellNavigationQueryParameters is read-only — use ContainsKey, not Remove.
    var isGoBack = query.ContainsKey("__isBack");
    SafeFireAndForget(ct => InitializeFromQueryAsync(query, isGoBack, ct), showLoader: true);
}
```

Shell calls `ApplyQueryAttributes` (via `BasePage`) both when navigating *to* a page and when navigating *back* to it. The `isGoBack` flag lets `InitializeFromQueryAsync` know which case it is, so the ViewModel can skip an expensive data fetch when the user is just returning from a child screen.

The sentinel is *key presence*, not key value. `query.ContainsKey("__isBack")` returns `true` if the key is present — that boolean becomes `isGoBack`. The value stored under `"__isBack"` is irrelevant. `Remove` cannot be used here because `ShellNavigationQueryParameters` is a read-only dictionary type; calling `Remove` on it throws at runtime.

### IsBusy ref-counting with `Interlocked`

```csharp
Interlocked.Increment(ref _busyCount);
IsBusy = true;
// ...
if (showLoader && Interlocked.Decrement(ref _busyCount) <= 0)
    IsBusy = false;
```

`IsBusy` is not a simple boolean flip. It is backed by a ref-counted integer. If two concurrent `SafeFireAndForget` calls are running simultaneously, `IsBusy` stays `true` until *both* complete. A simple `IsBusy = false` in the `finally` would turn off the loading indicator when the first call finishes, even though the second is still running. The ref-count prevents this.

`Interlocked.Increment` and `Interlocked.Decrement` are lock-free atomic operations. The busy count is updated correctly even if two tasks complete on different threads simultaneously.

### SafeFireAndForget / ExecuteSafeAsync

```csharp
protected void SafeFireAndForget(Func<CancellationToken, Task> action, bool showLoader = false)
    => _ = ExecuteSafeAsync(action, showLoader);
```

`SafeFireAndForget` discards the `Task` (the `_ =` assignment silences the "unawaited task" warning) but does not abandon it. The task still runs; exceptions are caught in `ExecuteSafeAsync` and logged. `OperationCanceledException` is silently swallowed — cancellation is expected, not an error.

The pattern is appropriate for UI-triggered async work (page load, data fetch) where the caller cannot `await` because it originates from a synchronous lifecycle method.

---

## 6. BasePage\<TViewModel\>

```csharp
// Base/BasePage.cs
using Microsoft.Maui.Controls;
using MauiNavigation.Core.Base;
using MauiNavigation.Core.Navigation;

namespace MauiNavigation.Base;

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
        // Fire-and-forget — MAUI's OnDisappearing cannot be async.
        // Do not rely on OnDisappearingInternal completing before the next page's OnAppearing fires.
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

    public void Dispose()
    {
        if (BindingContext is IDisposable disposable)
            disposable.Dispose();
        GC.SuppressFinalize(this);
    }
}
```

`BasePage` is the bridge layer between MAUI's framework concerns and the framework-agnostic Core. It lives in the App project, not in Core, because it directly references `ContentPage`, `IQueryAttributable`, and `Shell` — all from `Microsoft.Maui.Controls`.

### Why `IQueryAttributable` belongs here, not in Core

`IQueryAttributable` is defined in `Microsoft.Maui.Controls`. If `INavigationParameterReceiver` (which *is* in Core) tried to extend `IQueryAttributable`, Core would need a reference to `Microsoft.Maui.Controls`. The split would collapse.

Instead, `BasePage` implements `IQueryAttributable` and immediately forwards to `ViewModel.ApplyNavigationParameters(query)`. The ViewModel never knows about `IQueryAttributable` — it only knows about `INavigationParameterReceiver`, which is defined in Core.

### The ApplyQueryAttributes timing gap

`ApplyQueryAttributes` is called by Shell when navigating *to* a page, and also when navigating *back* to a page (Shell injects `__isBack`). However, it is **not called** when a modal is dismissed above this page. When `PopModalAsync` completes, MAUI calls `OnAppearing` on the page below, not `ApplyQueryAttributes`.

The practical consequence: post-dismiss refresh logic must go in `OnAppearingAsync`, not `InitializeFromQueryAsync`. `BrowseViewModel` handles this naturally — it checks `Movies.Count == 0` in `OnAppearingAsync`, so any re-appear triggers a check regardless of how the re-appear happened. In a real app with a shared filter service, `OnAppearingAsync` would read the current filter state from that service on every appear.

---

## 7. AppShell: TabBar + Route Registration

### The XAML (`AppShell.xaml`)

```xml
<?xml version="1.0" encoding="UTF-8" ?>
<Shell
    x:Class="MauiNavigation.AppShell"
    xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
    xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
    xmlns:browse="clr-namespace:MauiNavigation.Pages.Browse"
    xmlns:favorites="clr-namespace:MauiNavigation.Pages.Favorites">

    <TabBar>
        <Tab Title="Browse">
            <ShellContent
                Route="BrowsePage"
                ContentTemplate="{DataTemplate browse:BrowsePage}" />
        </Tab>
        <Tab Title="Favorites">
            <ShellContent
                Route="FavoritesPage"
                ContentTemplate="{DataTemplate favorites:FavoritesPage}" />
        </Tab>
    </TabBar>

</Shell>
```

The `ContentTemplate` with `DataTemplate` is important. Using `DataTemplate` means Shell does not instantiate `BrowsePage` until the user first switches to that tab — lazy initialization. Using `Content` (direct instance) would instantiate both pages at startup.

### The code-behind (`AppShell.xaml.cs`)

```csharp
// AppShell.xaml.cs
using MauiNavigation.Core.Navigation;
using MauiNavigation.Pages.MovieDetail;
using MauiNavigation.Pages.Filter;

namespace MauiNavigation;

public partial class AppShell : Shell
{
    public AppShell(INavigationService navigationService)
    {
        InitializeComponent();

        // Push routes — Shell resolves these via GoToAsync(nameof(...))
        // nameof(MovieDetailPage) == Routes.MovieDetail == "MovieDetailPage"
        Routing.RegisterRoute(nameof(MovieDetailPage), typeof(MovieDetailPage));

        // Modal routes — bypass Shell routing, resolved by ShellNavigationService
        // nameof(FilterPage) == Routes.Filter == "FilterPage"
        navigationService.RegisterModal(nameof(FilterPage), typeof(FilterPage));
    }
}
```

### Two registration paths

There are two entirely different mechanisms at work:

**`Routing.RegisterRoute(nameof(MovieDetailPage), typeof(MovieDetailPage))`**

This is MAUI Shell's built-in route registry. When `Shell.Current.GoToAsync("MovieDetailPage")` is called, Shell looks up this registry, resolves the page type, creates an instance (from DI if available), and pushes it onto the navigation stack. Shell also calls `ApplyQueryAttributes` on the new page automatically.

**`navigationService.RegisterModal(nameof(FilterPage), typeof(FilterPage))`**

This registers the page in `ShellNavigationService`'s own private dictionary. Modals are presented via `PushModalAsync`, which bypasses Shell's route resolution entirely. Shell never calls `ApplyQueryAttributes` for modals. This is why `ShellNavigationService.PresentModalAsync<TParams>` calls `ApplyNavigationParameters` directly on the ViewModel before pushing.

> **The critical invariant:** `nameof(MovieDetailPage)` is evaluated at compile time to the string `"MovieDetailPage"`. `Routes.MovieDetail` is the constant `"MovieDetailPage"`. Both must be identical. If you rename `MovieDetailPage` to `FilmDetailPage`, you must also update `Routes.MovieDetail`. The compiler cannot enforce this relationship — it is a runtime contract.

---

## 8. ShellNavigationService

```csharp
// Infrastructure/ShellNavigationService.cs
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Controls;
using MauiNavigation.Core.Navigation;

namespace MauiNavigation.Infrastructure;

public class ShellNavigationService(IServiceProvider serviceProvider) : INavigationService
{
    private readonly Dictionary<string, Type> _modalRegistry = new();

    public void RegisterModal(string route, Type pageType)
        => _modalRegistry[route] = pageType;

    public Task GoToAsync(string route, bool animated = true)
        => Shell.Current.GoToAsync(route, animated);

    public Task GoToAsync<TParams>(string route, TParams parameters, bool animated = true) where TParams : class
    {
        var shellParams = new ShellNavigationQueryParameters { ["__params"] = parameters };
        return Shell.Current.GoToAsync(route, animated, shellParams);
    }

    /// <summary>
    /// Navigates back and injects __isBack=true so the parent ViewModel can
    /// skip re-fetching data in InitializeFromQueryAsync.
    /// </summary>
    public Task GoBackAsync(bool animated = true)
    {
        var shellParams = new ShellNavigationQueryParameters { ["__isBack"] = true };
        return Shell.Current.GoToAsync("..", animated, shellParams);
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

    private ContentPage ResolveModalPage(string route)
    {
        if (!_modalRegistry.TryGetValue(route, out var pageType))
            throw new InvalidOperationException(
                $"No modal registered for route '{route}'. " +
                $"Call navigationService.RegisterModal(\"{route}\", typeof(YourPage)) from AppShell.");

        return (ContentPage)serviceProvider.GetRequiredService(pageType);
    }
}
```

### GoBackAsync

```csharp
public Task GoBackAsync(bool animated = true)
{
    var shellParams = new ShellNavigationQueryParameters { ["__isBack"] = true };
    return Shell.Current.GoToAsync("..", animated, shellParams);
}
```

Shell's `".."` route means "navigate back one level". The `__isBack` key is injected into the parameters so the parent page receives it in `ApplyQueryAttributes`. The parent ViewModel reads this in `ApplyNavigationParameters` to set `isGoBack = true`, allowing `InitializeFromQueryAsync` to skip a data reload.

### GoToAsync\<TParams\>

```csharp
public Task GoToAsync<TParams>(string route, TParams parameters, bool animated = true) where TParams : class
{
    var shellParams = new ShellNavigationQueryParameters { ["__params"] = parameters };
    return Shell.Current.GoToAsync(route, animated, shellParams);
}
```

`ShellNavigationQueryParameters` is a MAUI dictionary type specifically designed to pass object references through Shell navigation without URL-encoding them. The `"__params"` key is the convention used throughout this app to carry the typed parameter object to the destination.

### PresentModalAsync\<TParams\>

```csharp
public Task PresentModalAsync<TParams>(string route, TParams parameters, bool animated = true) where TParams : class
{
    var page = ResolveModalPage(route);
    if (page.BindingContext is INavigationParameterReceiver receiver)
        receiver.ApplyNavigationParameters(new Dictionary<string, object> { ["__params"] = parameters });
    return Shell.Current.Navigation.PushModalAsync(page, animated);
}
```

`PushModalAsync` does not go through Shell's routing system. Shell does not call `ApplyQueryAttributes` for modal pages. The service therefore calls `ApplyNavigationParameters` directly on the ViewModel (accessed via `BindingContext`) *before* pushing the page. This fires `InitializeFromQueryAsync` asynchronously before the page becomes visible — correct behavior since the page will appear after `PushModalAsync`.

### ResolveModalPage

```csharp
private ContentPage ResolveModalPage(string route)
{
    if (!_modalRegistry.TryGetValue(route, out var pageType))
        throw new InvalidOperationException(...);

    return (ContentPage)serviceProvider.GetRequiredService(pageType);
}
```

Pages are resolved from the DI container, which means a fresh `FilterPage` (and a fresh `FilterViewModel`) is created each time the modal is presented. This is the correct behavior for Transient registrations. If you need to carry state across multiple presentations of the same modal, put that state in a service with a longer lifetime.

---

## 9. DI Wiring in MauiProgram

### MauiProgram.cs

```csharp
// MauiProgram.cs
public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseMauiCommunityToolkit()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        // Infrastructure — singletons live for the app lifetime
        builder.Services.AddSingleton<INavigationService, ShellNavigationService>();
        builder.Services.AddSingleton<BaseViewModelFacade>();
        builder.Services.AddSingleton<AppShell>();

        // ViewModels — Transient: fresh instance per navigation push
        builder.Services.AddTransient<BrowseViewModel>();
        builder.Services.AddTransient<FavoritesViewModel>();
        builder.Services.AddTransient<MovieDetailViewModel>();
        builder.Services.AddTransient<FilterViewModel>();

        // Pages — Transient: lifetime must match ViewModel
        builder.Services.AddTransient<BrowsePage>();
        builder.Services.AddTransient<FavoritesPage>();
        builder.Services.AddTransient<MovieDetailPage>();
        builder.Services.AddTransient<FilterPage>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
```

### App.xaml.cs

```csharp
// App.xaml.cs
namespace MauiNavigation;

public partial class App : Application
{
    public App(IServiceProvider services)
    {
        InitializeComponent();
        MainPage = services.GetRequiredService<AppShell>();
    }
}
```

### Lifetime decisions explained

**Infrastructure as Singleton**

`INavigationService` / `ShellNavigationService` holds the modal registry (a `Dictionary<string, Type>`). If it were Transient, a new instance would be created for each resolution — the modal registry populated in `AppShell`'s constructor would be on a different instance than the one used when `PresentModalAsync` is called. Singleton ensures one registry for the app lifetime.

`BaseViewModelFacade` is Singleton because it holds no per-ViewModel state. Registering it Singleton means all Transient ViewModels share one facade instance — correct and efficient.

`AppShell` is Singleton because there is only one shell in the app. Shell manages its own navigation stack; creating a second instance would produce a second disconnected navigation stack.

**ViewModels and Pages as Transient**

Each navigation push should produce a fresh ViewModel with clean state. If `MovieDetailViewModel` were Singleton, navigating to two different movies would reuse the same ViewModel instance — the second movie's data would overwrite the first, but more importantly, if the user navigated back to the first movie, the ViewModel would show the second movie's data.

Pages are registered Transient because their lifetime must match their ViewModel's lifetime. A Singleton page holds a reference to its ViewModel forever. If the ViewModel is Transient, a new one is created for each navigation push — but the Singleton page still has the old ViewModel as its `BindingContext`. The new ViewModel's properties will never appear in the UI.

**Why App receives IServiceProvider**

```csharp
public App(IServiceProvider services)
{
    MainPage = services.GetRequiredService<AppShell>();
}
```

`AppShell` has a constructor parameter (`INavigationService`), so it cannot be instantiated with `new AppShell()`. MAUI's `UseMauiApp<App>()` creates the `App` instance via DI, so `App` can receive `IServiceProvider` in its constructor and use it to resolve `AppShell` from the container.

> **Senior tip:** Resolving `AppShell` from `IServiceProvider` directly (service locator pattern) is acceptable here because this is the composition root — the single place in the application where the object graph is assembled. Using service locator outside the composition root is a code smell; using it *at* the composition root is standard practice.

---

## 10. Real-World Walk-Through

### Flow A: Browse → MovieDetail → Back

**Step 1 — BrowsePage appears**

```
App starts → Shell loads BrowsePage (lazy via DataTemplate)
→ BrowsePage.OnAppearing()
→ BrowseViewModel.OnAppearingInternal()
→ BrowseViewModel.OnAppearingAsync(ct)
```

`BrowseViewModel.OnAppearingAsync` checks `Movies.Count == 0` and populates the list. On subsequent appears (returning from MovieDetail), the count is non-zero so the reload is skipped.

**Step 2 — User taps a movie**

```csharp
// BrowseViewModel.cs
[RelayCommand]
private Task SelectMovie(Movie movie) =>
    Facade.Navigation.GoToAsync(Routes.MovieDetail, new MovieDetailParameters(movie.Id, movie.Title));
```

`GoToAsync` wraps the parameters in `ShellNavigationQueryParameters["__params"]` and calls `Shell.Current.GoToAsync("MovieDetailPage", ...)`.

**Step 3 — Shell navigates to MovieDetailPage**

Shell resolves `MovieDetailPage` from the DI container (Transient → new instance), pushes it, then calls `ApplyQueryAttributes` on the new page.

```
MovieDetailPage.ApplyQueryAttributes(query)
→ MovieDetailViewModel.ApplyNavigationParameters(query)
→ isGoBack = query.ContainsKey("__isBack")  // false, key not present
→ SafeFireAndForget(ct => InitializeFromQueryAsync(query, isGoBack=false, ct))
```

```csharp
// MovieDetailViewModel.cs
public override Task InitializeFromQueryAsync(IDictionary<string, object> query, bool isGoBack, CancellationToken ct)
{
    if (isGoBack) return Task.CompletedTask;  // not triggered on forward navigation

    if (query.TryGetValue("__params", out var p) && p is MovieDetailParameters parms)
    {
        MovieId = parms.MovieId;
        Title = parms.Title;
    }

    return Task.CompletedTask;
}
```

**Step 4 — User taps Back**

```csharp
// MovieDetailViewModel.cs
[RelayCommand]
private Task GoBack() => Facade.Navigation.GoBackAsync();
```

`GoBackAsync` calls `Shell.Current.GoToAsync("..", shellParams)` where `shellParams` contains `["__isBack"] = true`.

Shell pops MovieDetailPage and calls `ApplyQueryAttributes` on BrowsePage with the `__isBack` key present.

```
BrowsePage.ApplyQueryAttributes({"__isBack": true})
→ BrowseViewModel.ApplyNavigationParameters({"__isBack": true})
→ isGoBack = query.ContainsKey("__isBack")  // true
→ SafeFireAndForget(ct => InitializeFromQueryAsync(query, isGoBack=true, ct))
```

`BrowseViewModel` does not override `InitializeFromQueryAsync` — the base returns `Task.CompletedTask`. The movie list is not reloaded. Shortly after, `BrowsePage.OnAppearing` fires and `OnAppearingAsync` sees `Movies.Count > 0` and also skips the reload. The user sees their list immediately.

---

### Flow B: Browse → Filter modal → Dismiss

**Step 1 — User taps Filter**

```csharp
// BrowseViewModel.cs
[RelayCommand]
private Task OpenFilter() =>
    Facade.Navigation.PresentModalAsync(Routes.Filter);
```

**Step 2 — ShellNavigationService resolves and pushes the modal**

```
PresentModalAsync("FilterPage")
→ ResolveModalPage("FilterPage")
  → serviceProvider.GetRequiredService<FilterPage>()  // new Transient instance
→ Shell.Current.Navigation.PushModalAsync(filterPage)
```

Shell does not call `ApplyQueryAttributes` for modals. No parameters are passed in this flow, so no direct `ApplyNavigationParameters` call is needed.

**Step 3 — FilterPage appears**

```
FilterPage.OnAppearing()
→ FilterViewModel.OnAppearingInternal()
→ FilterViewModel.OnAppearingAsync(ct)
```

In a real app, `OnAppearingAsync` would load the current filter state from a shared service.

**Step 4 — User taps Apply or Dismiss**

```csharp
// FilterViewModel.cs
[RelayCommand]
private Task Apply()
{
    // In a real app: save filter state to a service before dismissing
    return Facade.Navigation.DismissModalAsync();
}

[RelayCommand]
private Task Dismiss() => Facade.Navigation.DismissModalAsync();
```

`DismissModalAsync` calls `Shell.Current.Navigation.PopModalAsync()`.

**Step 5 — BrowsePage re-appears**

MAUI calls `OnAppearing` on `BrowsePage`. It does **not** call `ApplyQueryAttributes`.

```
BrowsePage.OnAppearing()
→ BrowseViewModel.OnAppearingInternal()
→ BrowseViewModel.OnAppearingAsync(ct)
```

This is where the post-dismiss refresh lives. In the example app, `OnAppearingAsync` checks `Movies.Count == 0` — no reload needed since the list is already populated. In a real app, this is where you would read the updated filter from the shared service and refresh the list accordingly.

> **Note:** `InitializeFromQueryAsync` is not called anywhere in this flow. It is only called from `ApplyNavigationParameters`, which is only called from `ApplyQueryAttributes` (for push navigation) or directly from `PresentModalAsync<TParams>` (for modal parameter delivery). Neither happens during a modal dismiss.

---

## 11. Gotchas and Tips

### Transient vs Singleton: pages and ViewModels must match

If a Page is registered as Singleton but its ViewModel is Transient, the Singleton page holds a permanent reference to the *first* ViewModel instance created. Every subsequent navigation push creates a new ViewModel (correct), but the page still has the original ViewModel as its `BindingContext` (wrong). The UI will never reflect the new ViewModel's state.

The rule: **Page and ViewModel lifetimes must match.** In this app both are Transient.

The tab pages (`BrowsePage`, `FavoritesPage`) could be Singleton since they are never pushed — they are instantiated by Shell's `DataTemplate` once and reused. The example registers them as Transient to keep the rule simple and uniform. Either is correct; the important constraint is that if you make the page Singleton, the ViewModel must also be Singleton.

### Modal vs push: different parameter delivery paths

| Navigation type | Shell routing | `ApplyQueryAttributes` called | Parameter delivery |
|---|---|---|---|
| `GoToAsync` (push) | Yes | Yes | `ShellNavigationQueryParameters["__params"]` |
| `GoBackAsync` | Yes | Yes (on parent) | `ShellNavigationQueryParameters["__isBack"]` |
| `PresentModalAsync` | No (PushModalAsync) | No | Direct `ApplyNavigationParameters` call |
| `DismissModalAsync` | No (PopModalAsync) | No | None — use `OnAppearingAsync` |

### `isGoBack` is only for push back-navigation

`isGoBack = true` is set when `GoBackAsync` injects `__isBack` into `ShellNavigationQueryParameters` and Shell delivers it to the parent page via `ApplyQueryAttributes`. This only happens in the push/back navigation path.

`DismissModalAsync` (`PopModalAsync`) does not trigger `ApplyQueryAttributes` on the page below. If you check `isGoBack` in `InitializeFromQueryAsync` expecting it to be `true` after a modal dismiss, it will not be — `InitializeFromQueryAsync` is not called at all after a modal dismiss. Post-dismiss refresh belongs in `OnAppearingAsync`.

### `nameof()` and Routes must stay in sync

`Routes.MovieDetail = "MovieDetailPage"` and `Routing.RegisterRoute(nameof(MovieDetailPage), ...)` must produce the same string. Currently both are `"MovieDetailPage"`. If the page class is renamed:

1. Update `Routes.MovieDetail` to the new class name string
2. `nameof(MovieDetailPage)` in `AppShell.xaml.cs` will automatically update (it is evaluated at compile time)

A test that calls `Shell.Current.GoToAsync(Routes.MovieDetail)` and verifies navigation occurs (or a simpler string comparison test) will catch this drift.

### IsBusy and nested async calls

Because `IsBusy` is ref-counted, you can call `SafeFireAndForget` multiple times concurrently and the loading indicator stays visible until all of them complete. A page that loads movies and user preferences in parallel:

```csharp
protected override Task OnAppearingAsync(CancellationToken ct)
{
    SafeFireAndForget(LoadMoviesAsync, showLoader: true);
    SafeFireAndForget(LoadPreferencesAsync, showLoader: true);
    return Task.CompletedTask;
}
```

`IsBusy` is `true` from the first `SafeFireAndForget` call until the last one completes — not until the first one completes.

### CancellationToken: always pass it down

`OnAppearingAsync` receives a `CancellationToken` that is cancelled when the page disappears. Pass it to every awaitable call in your override:

```csharp
protected override async Task OnAppearingAsync(CancellationToken ct)
{
    var movies = await _movieService.GetMoviesAsync(ct);  // pass ct
    Movies = new ObservableCollection<Movie>(movies);
}
```

If the user navigates away before the fetch completes, the `OperationCanceledException` is caught and swallowed by `OnAppearingInternal`. The ViewModel is not updated with stale data, and no exception is logged.

### Disposal

`BasePage.Dispose` calls `ViewModel.Dispose()`. MAUI does **not** call `Dispose` on pages automatically — the `CancellationTokenSource` is cancelled reliably through `OnDisappearing`, not through disposal. `Dispose` exists as a belt-and-suspenders path for explicit teardown in tests or custom cleanup scenarios.

If you hold other disposable resources in a ViewModel (timers, observables, event subscriptions), override `Dispose` and clean them up there:

```csharp
public override void Dispose()
{
    _myTimer?.Dispose();
    base.Dispose();  // cancels and disposes the CTS
}
```

---

*This article covers the navigation architecture as implemented in this repository. The same pattern scales to larger apps: add more routes to `Routes.cs`, register more pages in `AppShell.xaml.cs` and `MauiProgram.cs`, and add ViewModels that override `OnAppearingAsync` and `InitializeFromQueryAsync` as needed.*

---

## 12. Error Handling and User Feedback

Production apps need to surface errors to users. The architecture includes an `IAlertService` abstraction in Core with implementation in the App layer.

### IAlertService Interface

```csharp
// MauiNavigation.Core/Services/IAlertService.cs
public interface IAlertService
{
    Task ShowAlertAsync(string title, string message, string cancel = "OK");
    Task<bool> ShowConfirmAsync(string title, string message, string accept = "Yes", string cancel = "No");
    Task ShowToastAsync(string message, double durationSeconds = 3.0);
    Task ShowErrorAsync(string message, string? title = null);
}
```

### Implementation

```csharp
// MauiNavigation/Infrastructure/AlertService.cs
public class AlertService : IAlertService
{
    public async Task ShowAlertAsync(string title, string message, string cancel = "OK")
    {
        var page = Application.Current?.Windows.FirstOrDefault()?.Page;
        if (page is not null)
            await page.DisplayAlertAsync(title, message, cancel);
    }

    public async Task<bool> ShowConfirmAsync(string title, string message, 
        string accept = "Yes", string cancel = "No")
    {
        var page = Application.Current?.Windows.FirstOrDefault()?.Page;
        if (page is null) return false;
        return await page.DisplayAlertAsync(title, message, accept, cancel);
    }

    public async Task ShowErrorAsync(string message, string? title = null)
    {
        await ShowAlertAsync(title ?? "Error", message, "OK");
    }

    public Task ShowToastAsync(string message, double durationSeconds = 3.0)
    {
        // Uses CommunityToolkit.Maui toasts
        var toast = Toast.Make(message, ToastDuration.Short);
        return toast.Show();
    }
}
```

### Integration with BaseViewModelFacade

```csharp
public class BaseViewModelFacade
{
    public INavigationService Navigation { get; }
    public IAlertService Alerts { get; }  // <-- Added

    public BaseViewModelFacade(INavigationService navigation, IAlertService alerts)
    {
        Navigation = navigation;
        Alerts = alerts;
    }
}
```

### Safe Execution with Error Callbacks

`BaseViewModel.ExecuteSafeAsync` now accepts an optional error callback:

```csharp
protected async Task<bool> ExecuteSafeAsync(
    Func<CancellationToken, Task> action,
    CancellationToken cancellationToken = default,
    bool showLoader = true,
    Action<Exception>? onError = null)  // <-- Optional error handler
{
    try
    {
        if (showLoader) BeginLoading();
        await action(cancellationToken);
        return true;
    }
    catch (OperationCanceledException)
    {
        return false;
    }
    catch (Exception ex)
    {
        onError?.Invoke(ex);  // <-- Called on error
        return false;
    }
    finally
    {
        if (showLoader) EndLoading();
    }
}
```

### Convenient Helper: SafeFireAndForgetWithErrorAlert

For fire-and-forget calls that should show an alert on error:

```csharp
protected void SafeFireAndForgetWithErrorAlert(
    Func<CancellationToken, Task> action,
    string errorMessage = "An error occurred",
    bool showLoader = true)
{
    SafeFireAndForget(action, showLoader, onError: ex =>
    {
        MainThread.BeginInvokeOnMainThread(async () =>
            await Facade.Alerts.ShowErrorAsync(errorMessage));
    });
}
```

### Usage Example

```csharp
public class MovieDetailViewModel : BaseViewModel<MovieDetailViewModel>
{
    protected override Task OnAppearingAsync(CancellationToken ct)
    {
        SafeFireAndForgetWithErrorAlert(
            LoadMovieDetailsAsync,
            errorMessage: "Failed to load movie details"
        );
        return Task.CompletedTask;
    }

    private async Task LoadMovieDetailsAsync(CancellationToken ct)
    {
        // If this throws, user sees "Failed to load movie details" alert
        var movie = await _movieService.GetMovieAsync(_movieId, ct);
        Movie = movie;
    }
}
```

### Decision Tree: Toast vs Alert

| Scenario | Use |
|----------|-----|
| Background operation succeeded | Toast |
| Non-critical warning | Toast |
| User needs to acknowledge an error | Alert |
| User needs to make a decision | Confirm alert |
| Network error with retry option | Alert with action |

---

## 13. Returning Results from Modals

A common pattern is presenting a modal that returns a result when dismissed. For example, a filter page that returns the selected filters.

### NavigationResult<T>

```csharp
// MauiNavigation.Core/Navigation/NavigationResult.cs
public class NavigationResult<TResult>
{
    public bool Succeeded { get; }
    public TResult? Value { get; }

    private NavigationResult(bool succeeded, TResult? value)
    {
        Succeeded = succeeded;
        Value = value;
    }

    public static NavigationResult<TResult> Success(TResult value) 
        => new(true, value);

    public static NavigationResult<TResult> Cancelled() 
        => new(false, default);

    public bool TryGetValue(out TResult? value)
    {
        value = Value;
        return Succeeded;
    }
}
```

### IModalResultProvider<TResult>

Modals that return results implement this interface:

```csharp
// MauiNavigation.Core/Navigation/IModalResultProvider.cs
public interface IModalResultProvider<TResult>
{
    void SetResultCompletion(TaskCompletionSource<NavigationResult<TResult>> completion);
}
```

### INavigationService Extensions

```csharp
public interface INavigationService
{
    // Existing methods...

    /// <summary>
    /// Present a modal and await its result.
    /// Modal ViewModel must implement IModalResultProvider.
    /// </summary>
    Task<NavigationResult<TResult>> PresentModalForResultAsync<TResult>(
        string route, bool animated = true);

    Task<NavigationResult<TResult>> PresentModalForResultAsync<TParams, TResult>(
        string route, TParams parameters, bool animated = true) 
        where TParams : class;
}
```

### Modal ViewModel Implementation

```csharp
public class FilterViewModel : BaseViewModel<FilterViewModel>, 
    IModalResultProvider<FilterResult>
{
    private TaskCompletionSource<NavigationResult<FilterResult>>? _completion;
    
    public string? SelectedGenre { get; set; }
    public int? MinYear { get; set; }

    public void SetResultCompletion(
        TaskCompletionSource<NavigationResult<FilterResult>> completion)
    {
        _completion = completion;
    }

    [RelayCommand]
    private async Task Apply()
    {
        var result = new FilterResult(SelectedGenre, MinYear);
        _completion?.SetResult(NavigationResult<FilterResult>.Success(result));
        await Facade.Navigation.DismissModalAsync();
    }

    [RelayCommand]
    private async Task Cancel()
    {
        _completion?.SetResult(NavigationResult<FilterResult>.Cancelled());
        await Facade.Navigation.DismissModalAsync();
    }
}
```

### Calling ViewModel Usage

```csharp
public class BrowseViewModel : BaseViewModel<BrowseViewModel>
{
    [RelayCommand]
    private async Task OpenFilter()
    {
        var result = await Facade.Navigation
            .PresentModalForResultAsync<FilterResult>(Routes.Filter);

        if (result.TryGetValue(out var filter))
        {
            // User applied filters
            SelectedGenre = filter.Genre;
            MinYear = filter.MinYear;
            await RefreshMoviesAsync();
        }
        // If Cancelled, do nothing
    }
}
```

### Flow Diagram

```
┌─────────────┐     PresentModalForResultAsync     ┌──────────────┐
│ BrowseVM    │ ─────────────────────────────────► │ FilterVM     │
│             │                                    │              │
│ (awaiting)  │                                    │ User selects │
│             │                                    │ filters      │
│             │     NavigationResult<Filter>       │              │
│             │ ◄───────────────────────────────── │ Apply()      │
│             │                                    │              │
│ Apply filter│                                    │ Dismissed    │
└─────────────┘                                    └──────────────┘
```

---

## 14. Tab Navigation

For apps with a TabBar, you often need to switch tabs programmatically or switch to a tab and then navigate within it.

### Tab Routes

Define tab routes with `//` prefix for absolute navigation:

```csharp
// MauiNavigation.Core/Navigation/Routes.cs
public static class Routes
{
    // Tab routes (absolute)
    public const string BrowseTab = "//BrowseTab";
    public const string FavoritesTab = "//FavoritesTab";

    // Page routes (relative)
    public const string MovieDetail = "MovieDetailPage";
    public const string Filter = "FilterPage";
}
```

### INavigationService Extensions

```csharp
public interface INavigationService
{
    /// <summary>
    /// Switch to a different tab without pushing any page.
    /// </summary>
    Task SwitchTabAsync(string tabRoute, bool animated = true);

    /// <summary>
    /// Switch to a tab and immediately navigate to a page within that tab.
    /// </summary>
    Task SwitchTabAndNavigateAsync<TParams>(
        string tabRoute, string pageRoute, TParams parameters, bool animated = true) 
        where TParams : class;
}
```

### Implementation

```csharp
public class ShellNavigationService : INavigationService
{
    public Task SwitchTabAsync(string tabRoute, bool animated = true)
    {
        // Absolute route switches tabs: "//BrowseTab"
        return Shell.Current.GoToAsync(tabRoute, animated);
    }

    public async Task SwitchTabAndNavigateAsync<TParams>(
        string tabRoute, string pageRoute, TParams parameters, bool animated = true) 
        where TParams : class
    {
        // Build combined route: "//BrowseTab/MovieDetailPage"
        var combinedRoute = $"{tabRoute}/{pageRoute}";
        var shellParams = ParameterHelper.ToShellParameters(parameters);
        await Shell.Current.GoToAsync(combinedRoute, animated, shellParams);
    }
}
```

### Usage Examples

```csharp
public class FavoritesViewModel : BaseViewModel<FavoritesViewModel>
{
    // Simple tab switch
    [RelayCommand]
    private async Task GoToBrowse()
    {
        await Facade.Navigation.SwitchTabAsync(Routes.BrowseTab);
    }

    // Switch tab and open a specific movie
    [RelayCommand]
    private async Task ViewFeaturedMovie()
    {
        await Facade.Navigation.SwitchTabAndNavigateAsync(
            Routes.BrowseTab,
            Routes.MovieDetail,
            new MovieDetailParams(42, "Featured Film"));
    }
}
```

### AppShell Configuration

```xml
<Shell xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
       x:Class="MauiNavigation.AppShell">
    <TabBar>
        <ShellContent x:Name="BrowseTab"
                      Title="Browse"
                      Route="BrowseTab"
                      ContentTemplate="{DataTemplate local:MainPage}" />
        <ShellContent x:Name="FavoritesTab"
                      Title="Favorites"
                      Route="FavoritesTab"
                      ContentTemplate="{DataTemplate local:FavoritesPage}" />
    </TabBar>
</Shell>
```

### Important Notes

1. **Absolute vs Relative Routes**: Use `//` prefix for absolute routes that switch tabs
2. **Route Registration**: Tab routes must match `Route` property in `ShellContent`
3. **Back Stack**: Switching tabs preserves each tab's navigation stack

---

## 15. Advanced Back Navigation

Sometimes you need to go back multiple levels or return to the root of the current tab.

### INavigationService Extensions

```csharp
public interface INavigationService
{
    /// <summary>
    /// Go back a specific number of levels in the navigation stack.
    /// </summary>
    Task GoBackAsync(int levels, bool animated = true);

    /// <summary>
    /// Pop all pages and return to the root page of the current tab.
    /// </summary>
    Task GoBackToRootAsync(bool animated = true);
}
```

### Implementation

```csharp
public class ShellNavigationService : INavigationService
{
    public Task GoBackAsync(int levels, bool animated = true)
    {
        // Build relative back route: "../.." for 2 levels
        var route = string.Join("/", Enumerable.Repeat("..", levels));
        return Shell.Current.GoToAsync(route, animated);
    }

    public Task GoBackToRootAsync(bool animated = true)
    {
        // Get current location and extract the root tab route
        var currentRoute = Shell.Current.CurrentState.Location.ToString();
        
        // Parse: "//BrowseTab/MovieDetailPage/ActorPage" → "//BrowseTab"
        var parts = currentRoute.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length > 0)
        {
            var rootRoute = "//" + parts[0];
            return Shell.Current.GoToAsync(rootRoute, animated);
        }

        return Shell.Current.GoToAsync("..", animated);
    }
}
```

### Usage Examples

```csharp
public class ActorDetailViewModel : BaseViewModel<ActorDetailViewModel>
{
    // Go back 2 levels: ActorDetail → Actor → MovieDetail
    [RelayCommand]
    private async Task BackToMovie()
    {
        await Facade.Navigation.GoBackAsync(2);
    }

    // Go all the way back to Browse tab root
    [RelayCommand]
    private async Task BackToHome()
    {
        await Facade.Navigation.GoBackToRootAsync();
    }
}
```

### When to Use

| Scenario | Method |
|----------|--------|
| Normal back button | `GoBackAsync()` |
| Skip intermediate page | `GoBackAsync(2)` |
| "Done" button in multi-step wizard | `GoBackAsync(steps)` or `GoBackToRootAsync()` |
| "Cancel" in deep flow | `GoBackToRootAsync()` |
| Explicit route after action | `GoToAsync("//BrowseTab")` |

---

## 16. Navigation Guards

Navigation guards prevent accidental data loss by confirming navigation when a page has unsaved changes.

### INavigationGuard Interface

```csharp
// MauiNavigation.Core/Navigation/INavigationGuard.cs
public interface INavigationGuard
{
    /// <summary>
    /// Returns true if the page has unsaved changes.
    /// Checked before showing confirmation dialog.
    /// </summary>
    bool HasUnsavedChanges { get; }

    /// <summary>
    /// Called when user tries to navigate away with unsaved changes.
    /// Return true to allow navigation, false to cancel.
    /// </summary>
    Task<bool> CanLeaveAsync();
}
```

### ViewModel Implementation

```csharp
public class FilterViewModel : BaseViewModel<FilterViewModel>, INavigationGuard
{
    private string? _originalGenre;
    private int? _originalMinYear;

    public string? SelectedGenre { get; set; }
    public int? MinYear { get; set; }

    public bool HasUnsavedChanges =>
        SelectedGenre != _originalGenre || MinYear != _originalMinYear;

    public async Task<bool> CanLeaveAsync()
    {
        return await Facade.Alerts.ShowConfirmAsync(
            "Discard Changes?",
            "You have unsaved changes. Do you want to discard them?",
            "Discard",
            "Stay");
    }

    protected override Task OnAppearingAsync(CancellationToken ct)
    {
        // Capture initial state to detect changes
        _originalGenre = SelectedGenre;
        _originalMinYear = MinYear;
        return Task.CompletedTask;
    }
}
```

### ShellNavigationService Integration

```csharp
public class ShellNavigationService : INavigationService
{
    public async Task GoBackAsync(bool animated = true)
    {
        if (!await CheckNavigationGuardAsync())
            return;  // Guard cancelled navigation

        await Shell.Current.GoToAsync("..", animated);
    }

    private async Task<bool> CheckNavigationGuardAsync()
    {
        var currentPage = Shell.Current.CurrentPage;
        if (currentPage?.BindingContext is INavigationGuard guard 
            && guard.HasUnsavedChanges)
        {
            return await guard.CanLeaveAsync();
        }
        return true;
    }
}
```

### Limitations

Navigation guards only work for programmatic navigation (`GoBackAsync`, `GoToAsync`). They do **not** intercept:
- Hardware back button (Android/iOS gesture)
- Shell's built-in back button in navigation bar

For hardware back button handling, you need platform-specific code or Shell's `BackButtonBehavior`:

```xml
<Shell.BackButtonBehavior>
    <BackButtonBehavior Command="{Binding BackCommand}" />
</Shell.BackButtonBehavior>
```

---

## 17. Deep Linking

Deep linking allows external apps, notifications, or web links to open specific pages in your app.

### IDeepLinkHandler Interface

```csharp
// MauiNavigation.Core/Navigation/IDeepLinkHandler.cs
public interface IDeepLinkHandler
{
    /// <summary>
    /// Handle an incoming deep link URI.
    /// </summary>
    Task HandleAsync(Uri uri);

    /// <summary>
    /// Check if this handler supports the given URI.
    /// </summary>
    bool CanHandle(Uri uri);
}
```

### Implementation

```csharp
// MauiNavigation/Infrastructure/DeepLinkHandler.cs
public class DeepLinkHandler : IDeepLinkHandler
{
    private readonly INavigationService _navigation;

    public DeepLinkHandler(INavigationService navigation)
    {
        _navigation = navigation;
    }

    public bool CanHandle(Uri uri)
    {
        return uri.Scheme == "mauinavigation" 
            || uri.Host == "mauinavigation.app";
    }

    public async Task HandleAsync(Uri uri)
    {
        var path = uri.AbsolutePath.TrimStart('/');
        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);

        if (segments.Length == 0)
        {
            await _navigation.SwitchTabAsync(Routes.BrowseTab);
            return;
        }

        switch (segments[0].ToLowerInvariant())
        {
            case "movie" when segments.Length > 1 && int.TryParse(segments[1], out var id):
                await _navigation.GoToAsync(Routes.MovieDetail, 
                    new MovieDetailParams(id, $"Movie {id}"));
                break;

            case "browse":
                await _navigation.SwitchTabAsync(Routes.BrowseTab);
                break;

            case "favorites":
                await _navigation.SwitchTabAsync(Routes.FavoritesTab);
                break;

            default:
                await _navigation.SwitchTabAsync(Routes.BrowseTab);
                break;
        }
    }
}
```

### App.xaml.cs Integration

```csharp
public partial class App : Application
{
    private readonly IDeepLinkHandler _deepLinkHandler;

    public App(IDeepLinkHandler deepLinkHandler)
    {
        _deepLinkHandler = deepLinkHandler;
        InitializeComponent();
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        return new Window(new AppShell());
    }

    protected override async void OnAppLinkRequestReceived(Uri uri)
    {
        base.OnAppLinkRequestReceived(uri);

        if (_deepLinkHandler.CanHandle(uri))
        {
            await _deepLinkHandler.HandleAsync(uri);
        }
    }
}
```

### Android Configuration

Add intent filters to `Platforms/Android/AndroidManifest.xml`:

```xml
<activity android:name="MauiNavigation.MainActivity" ...>
    <!-- Custom URL scheme: mauinavigation://movie/123 -->
    <intent-filter>
        <action android:name="android.intent.action.VIEW" />
        <category android:name="android.intent.category.DEFAULT" />
        <category android:name="android.intent.category.BROWSABLE" />
        <data android:scheme="mauinavigation" />
    </intent-filter>

    <!-- App Links: https://mauinavigation.app/movie/123 -->
    <intent-filter android:autoVerify="true">
        <action android:name="android.intent.action.VIEW" />
        <category android:name="android.intent.category.DEFAULT" />
        <category android:name="android.intent.category.BROWSABLE" />
        <data android:scheme="https" android:host="mauinavigation.app" />
    </intent-filter>
</activity>
```

### iOS Configuration

Add URL types to `Platforms/iOS/Info.plist`:

```xml
<key>CFBundleURLTypes</key>
<array>
    <dict>
        <key>CFBundleURLName</key>
        <string>com.yourcompany.mauinavigation</string>
        <key>CFBundleURLSchemes</key>
        <array>
            <string>mauinavigation</string>
        </array>
    </dict>
</array>
```

For Universal Links, also add Associated Domains entitlement and host an `apple-app-site-association` file.

### Testing Deep Links

**Android:**
```bash
adb shell am start -W -a android.intent.action.VIEW \
    -d "mauinavigation://movie/123" com.yourcompany.mauinavigation
```

**iOS Simulator:**
```bash
xcrun simctl openurl booted "mauinavigation://movie/123"
```

### Supported URI Formats

| URI | Action |
|-----|--------|
| `mauinavigation://movie/123` | Open movie detail for ID 123 |
| `mauinavigation://browse` | Switch to Browse tab |
| `mauinavigation://favorites` | Switch to Favorites tab |
| `https://mauinavigation.app/movie/123` | Open movie detail (App Links) |

---

## 18. Shared State Between Pages

When multiple pages need to share state (like filters, user preferences, or cart items), use a singleton service with events.

### IFilterService Interface

```csharp
// MauiNavigation.Core/Services/IFilterService.cs
public interface IFilterService
{
    FilterState CurrentFilter { get; }
    bool HasFilter { get; }
    void ApplyFilter(string? genre, int? minYear);
    void ClearFilter();
    event EventHandler<FilterChangedEventArgs>? FilterChanged;
}

public record FilterState(string? Genre, int? MinYear)
{
    public static FilterState Empty => new(null, null);
    public bool HasFilter => Genre is not null || MinYear is not null;
}

public class FilterChangedEventArgs : EventArgs
{
    public FilterState OldFilter { get; }
    public FilterState NewFilter { get; }
    
    public FilterChangedEventArgs(FilterState oldFilter, FilterState newFilter)
    {
        OldFilter = oldFilter;
        NewFilter = newFilter;
    }
}
```

### FilterService Implementation

```csharp
public class FilterService : IFilterService
{
    private FilterState _currentFilter = FilterState.Empty;

    public FilterState CurrentFilter => _currentFilter;
    public bool HasFilter => _currentFilter.HasFilter;

    public event EventHandler<FilterChangedEventArgs>? FilterChanged;

    public void ApplyFilter(string? genre, int? minYear)
    {
        var oldFilter = _currentFilter;
        var newFilter = new FilterState(genre, minYear);

        if (oldFilter == newFilter) return;

        _currentFilter = newFilter;
        FilterChanged?.Invoke(this, new FilterChangedEventArgs(oldFilter, newFilter));
    }

    public void ClearFilter()
    {
        if (!_currentFilter.HasFilter) return;

        var oldFilter = _currentFilter;
        _currentFilter = FilterState.Empty;
        FilterChanged?.Invoke(this, new FilterChangedEventArgs(oldFilter, FilterState.Empty));
    }
}
```

### Producer: FilterViewModel

```csharp
public class FilterViewModel : BaseViewModel<FilterViewModel>
{
    private readonly IFilterService _filterService;

    public FilterViewModel(BaseViewModelFacade facade, IFilterService filterService) 
        : base(facade)
    {
        _filterService = filterService;
    }

    [RelayCommand]
    private async Task Apply()
    {
        _filterService.ApplyFilter(SelectedGenre, MinYear);
        await Facade.Navigation.DismissModalAsync();
    }
}
```

### Consumer: BrowseViewModel

```csharp
public class BrowseViewModel : BaseViewModel<BrowseViewModel>, IDisposable
{
    private readonly IFilterService _filterService;

    public BrowseViewModel(BaseViewModelFacade facade, IFilterService filterService) 
        : base(facade)
    {
        _filterService = filterService;
        _filterService.FilterChanged += OnFilterChanged;
    }

    private void OnFilterChanged(object? sender, FilterChangedEventArgs e)
    {
        // React to filter changes from any source
        MainThread.BeginInvokeOnMainThread(() =>
        {
            SelectedGenre = e.NewFilter.Genre;
            MinYear = e.NewFilter.MinYear;
            SafeFireAndForget(RefreshMoviesAsync, showLoader: true);
        });
    }

    public override void Dispose()
    {
        _filterService.FilterChanged -= OnFilterChanged;
        base.Dispose();
    }
}
```

### DI Registration

```csharp
// MauiProgram.cs
builder.Services.AddSingleton<IFilterService, FilterService>();
```

### When to Use Shared Services vs Navigation Parameters

| Scenario | Approach |
|----------|----------|
| One-time data passing | Navigation parameters |
| Parent → Child data | Navigation parameters |
| Child → Parent result | Modal result pattern |
| Multiple pages reading same state | Shared service |
| State persists across navigation | Shared service |
| Real-time updates needed | Shared service with events |

### Memory Leak Prevention

Always unsubscribe from events in `Dispose()`:

```csharp
public override void Dispose()
{
    _filterService.FilterChanged -= OnFilterChanged;
    _userService.UserChanged -= OnUserChanged;
    base.Dispose();
}
```

---

## 19. Testing ViewModels

The architecture is designed for testability. ViewModels depend on interfaces, making them easy to mock.

### Test Project Structure

```
MauiNavigation.Tests/
├── Base/
│   └── BaseViewModelTests.cs
├── ViewModels/
│   ├── BrowseViewModelTests.cs
│   ├── MovieDetailViewModelTests.cs
│   └── FilterViewModelTests.cs
├── Services/
│   └── FilterServiceTests.cs
├── Navigation/
│   └── NavigationResultTests.cs
└── Mocks/
    ├── MockNavigationService.cs
    └── NullAlertService.cs
```

### MockNavigationService

```csharp
public class MockNavigationService : INavigationService
{
    private readonly List<NavigationCall> _calls = [];
    public IReadOnlyList<NavigationCall> Calls => _calls;

    public Task GoToAsync(string route, bool animated = true)
    {
        _calls.Add(new NavigationCall(NavigationType.GoTo, route, null, animated));
        return Task.CompletedTask;
    }

    public Task GoToAsync<TParams>(string route, TParams parameters, bool animated = true) 
        where TParams : class
    {
        _calls.Add(new NavigationCall(NavigationType.GoTo, route, parameters, animated));
        return Task.CompletedTask;
    }

    // ... other methods
}

public record NavigationCall(NavigationType Type, string? Route, object? Parameters, bool Animated);
```

### NullAlertService

```csharp
public class NullAlertService : IAlertService
{
    public Task ShowAlertAsync(string title, string message, string cancel = "OK") 
        => Task.CompletedTask;
    public Task<bool> ShowConfirmAsync(string title, string message, 
        string accept = "Yes", string cancel = "No") 
        => Task.FromResult(true);
    public Task ShowToastAsync(string message, double durationSeconds = 3.0) 
        => Task.CompletedTask;
    public Task ShowErrorAsync(string message, string? title = null) 
        => Task.CompletedTask;
}
```

### Testing Navigation Calls

```csharp
public class BrowseViewModelTests
{
    [Fact]
    public async Task SelectMovie_NavigatesToMovieDetail()
    {
        // Arrange
        var mockNav = new MockNavigationService();
        var facade = new BaseViewModelFacade(mockNav, new NullAlertService());
        var viewModel = new BrowseViewModel(facade, new FilterService());
        var movie = new Movie { Id = 42, Title = "Test Movie" };

        // Act
        await viewModel.SelectMovieCommand.ExecuteAsync(movie);

        // Assert
        var call = Assert.Single(mockNav.Calls);
        Assert.Equal(NavigationType.GoTo, call.Type);
        Assert.Equal(Routes.MovieDetail, call.Route);

        var param = Assert.IsType<MovieDetailParams>(call.Parameters);
        Assert.Equal(42, param.MovieId);
        Assert.Equal("Test Movie", param.MovieTitle);
    }
}
```

### Testing Parameter Extraction

```csharp
public class MovieDetailViewModelTests
{
    [Fact]
    public async Task InitializeFromQuery_ExtractsParameters()
    {
        // Arrange
        var facade = new BaseViewModelFacade(
            new MockNavigationService(), 
            new NullAlertService());
        var viewModel = new MovieDetailViewModel(facade);
        var query = new ShellNavigationQueryParameters
        {
            ["MovieId"] = "42",
            ["MovieTitle"] = "Test Movie"
        };

        // Act
        await viewModel.InitializeFromQueryAsyncPublic(query, CancellationToken.None);

        // Assert
        Assert.Equal("Test Movie", viewModel.Title);
    }
}
```

### Testing Lifecycle

```csharp
public class BaseViewModelTests
{
    [Fact]
    public async Task OnAppearingInternal_SetsIsBusyDuringLoad()
    {
        // Arrange
        var facade = new BaseViewModelFacade(
            new MockNavigationService(), 
            new NullAlertService());
        var viewModel = new TestViewModel(facade);
        bool wasBusy = false;

        viewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(BaseViewModel.IsBusy) && viewModel.IsBusy)
                wasBusy = true;
        };

        // Act
        await viewModel.OnAppearingInternalPublic(CancellationToken.None);

        // Assert
        Assert.True(wasBusy);
        Assert.False(viewModel.IsBusy); // Should be false after completion
    }
}
```

### Testing FilterService Events

```csharp
public class FilterServiceTests
{
    [Fact]
    public void ApplyFilter_RaisesFilterChangedEvent()
    {
        // Arrange
        var service = new FilterService();
        FilterChangedEventArgs? capturedArgs = null;
        service.FilterChanged += (_, args) => capturedArgs = args;

        // Act
        service.ApplyFilter("Sci-Fi", 2010);

        // Assert
        Assert.NotNull(capturedArgs);
        Assert.Equal(FilterState.Empty, capturedArgs.OldFilter);
        Assert.Equal("Sci-Fi", capturedArgs.NewFilter.Genre);
    }

    [Fact]
    public void ApplyFilter_DoesNotRaiseEvent_WhenValueUnchanged()
    {
        // Arrange
        var service = new FilterService();
        service.ApplyFilter("Drama", 2000);

        int callCount = 0;
        service.FilterChanged += (_, _) => callCount++;

        // Act
        service.ApplyFilter("Drama", 2000); // Same values

        // Assert
        Assert.Equal(0, callCount);
    }
}
```

### Running Tests

```bash
# Run all tests
dotnet test MauiNavigation.Tests

# Run with verbose output
dotnet test MauiNavigation.Tests -v n

# Run specific test class
dotnet test MauiNavigation.Tests --filter "FullyQualifiedName~FilterServiceTests"
```

---

## 20. Quick Reference

### Navigation Methods

| Method | Description | Example |
|--------|-------------|---------|
| `GoToAsync(route)` | Navigate to page | `GoToAsync(Routes.MovieDetail)` |
| `GoToAsync<T>(route, params)` | Navigate with parameters | `GoToAsync(Routes.MovieDetail, new MovieDetailParams(42, "Title"))` |
| `GoBackAsync()` | Go back one page | `GoBackAsync()` |
| `GoBackAsync(levels)` | Go back N pages | `GoBackAsync(2)` |
| `GoBackToRootAsync()` | Pop to tab root | `GoBackToRootAsync()` |
| `PresentModalAsync(route)` | Show modal | `PresentModalAsync(Routes.Filter)` |
| `DismissModalAsync()` | Dismiss modal | `DismissModalAsync()` |
| `PresentModalForResultAsync<T>(route)` | Modal with result | `PresentModalForResultAsync<FilterResult>(Routes.Filter)` |
| `SwitchTabAsync(tabRoute)` | Switch tabs | `SwitchTabAsync(Routes.BrowseTab)` |
| `SwitchTabAndNavigateAsync<T>` | Switch tab + navigate | `SwitchTabAndNavigateAsync(Routes.BrowseTab, Routes.MovieDetail, params)` |

### Lifecycle Methods

| Method | When Called | Use Case |
|--------|-------------|----------|
| `InitializeFromQueryAsync` | Before page appears | Extract navigation parameters |
| `OnAppearingAsync` | Page appears | Load data, start animations |
| `OnDisappearingAsync` | Page disappears | Pause operations |
| `Dispose` | Page disposed | Unsubscribe events, cleanup |

### Key Interfaces

| Interface | Purpose | Implement When |
|-----------|---------|----------------|
| `INavigationService` | Navigation abstraction | N/A (use provided) |
| `IAlertService` | User feedback | N/A (use provided) |
| `IFilterService` | Shared filter state | N/A (use provided) |
| `IModalResultProvider<T>` | Modal returns result | Creating result-returning modals |
| `INavigationGuard` | Prevent data loss | Page has unsaved changes |
| `IDeepLinkHandler` | Handle deep links | N/A (use provided) |

### Error Handling Patterns

```csharp
// Silent retry with logging
SafeFireAndForget(LoadDataAsync, showLoader: true);

// Show alert on error
SafeFireAndForgetWithErrorAlert(LoadDataAsync, "Failed to load data");

// Custom error handling
SafeFireAndForget(LoadDataAsync, showLoader: true, onError: ex =>
{
    LogError(ex);
    ShowRetryButton();
});
```

---

*This guide covers navigation patterns for .NET MAUI apps of any size. Start with the basics (Sections 1-11) and add advanced patterns (Sections 12-19) as your app grows. The example code in this repository demonstrates every pattern described here.*
