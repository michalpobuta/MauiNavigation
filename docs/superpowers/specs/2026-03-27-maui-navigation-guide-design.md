# MAUI Navigation Guide — Design Spec

**Date:** 2026-03-27
**Status:** Approved

---

## Overview

A production-grade .NET MAUI navigation guide delivered as two artifacts:

1. **Example app** — a Movies app (Browse + Favorites tabs, Movie Detail push, Filter modal) that demonstrates every pattern end-to-end.
2. **Article** (`NAVIGATION_GUIDE.md` at repo root) — structured for all levels: readable top-to-bottom by intermediates, with "why" rationale that seniors will find useful.

---

## Solution Structure

```
MauiNavigation.sln
├── MauiNavigation/                        # MAUI app (.NET 10, Android/iOS)
│   ├── MauiProgram.cs                     # DI wiring
│   ├── AppShell.xaml / .cs                # TabBar + route registration
│   ├── Infrastructure/
│   │   └── ShellNavigationService.cs      # INavigationService implementation
│   ├── Base/
│   │   └── BasePage.cs                    # BasePage<TViewModel>
│   └── Pages/
│       ├── Browse/
│       │   ├── BrowsePage.xaml + .cs
│       ├── Favorites/
│       │   ├── FavoritesPage.xaml + .cs
│       ├── MovieDetail/
│       │   ├── MovieDetailPage.xaml + .cs
│       └── Filter/                        # modal
│           ├── FilterPage.xaml + .cs
│
└── MauiNavigation.Core/                   # net10.0 class library — zero MAUI refs
    ├── Navigation/
    │   ├── INavigationService.cs
    │   ├── INavigationParameterReceiver.cs
    │   └── Parameters/
    │       ├── MovieDetailParameters.cs   # record
    │       └── FilterParameters.cs        # record
    ├── Base/
    │   ├── BaseViewModelFacade.cs
    │   └── BaseViewModel.cs
    ├── ViewModels/
    │   ├── BrowseViewModel.cs
    │   ├── FavoritesViewModel.cs
    │   ├── MovieDetailViewModel.cs
    │   └── FilterViewModel.cs
    └── Models/
        └── Movie.cs
```

**Key constraint:** `MauiNavigation.Core` is a `net10.0` class library with no reference to `Microsoft.Maui.Controls`. This is enforced at compile time, not by convention.

---

## Navigation Contract

### `INavigationService` (in Core)

```csharp
public interface INavigationService
{
    void RegisterModal(string route, Type pageType);
    Task GoToAsync(string route, bool animated = true);
    Task GoToAsync<TParams>(string route, TParams parameters, bool animated = true) where TParams : class;
    Task GoBackAsync(bool animated = true);
    Task PresentModalAsync(string route, bool animated = true);
    Task PresentModalAsync<TParams>(string route, TParams parameters, bool animated = true) where TParams : class;
    Task DismissModalAsync(bool animated = true);
}
```

### Parameter Flow

Parameters travel as a typed object under a reserved key `"__params"` in Shell's `ShellNavigationQueryParameters`. No reflection, no string serialization — the object is passed through intact.

**Sending (ShellNavigationService):**
```csharp
var shellParams = new ShellNavigationQueryParameters { ["__params"] = parameters };
await Shell.Current.GoToAsync(route, animated, shellParams);
```

**Receiving (ViewModel via INavigationParameterReceiver):**
```csharp
public override Task InitializeFromQueryAsync(IDictionary<string, object> query, bool isGoBack, CancellationToken ct)
{
    if (query.TryGetValue("__params", out var p) && p is MovieDetailParameters parms)
    { ... }
}
```

### Strongly-Typed Parameter Records (in Core)

```csharp
public record MovieDetailParameters(int MovieId, string Title);
public record FilterParameters(string? Genre, int? MinYear);
```

### Route Registration

All routes registered in `AppShell.xaml.cs` using `nameof()`. All call sites also use `nameof()` — no magic strings anywhere.

```csharp
// Registration (once) — only Shell-routed pages; FilterPage is NOT registered here
// because it is presented as a modal via Navigation.PushModalAsync, not GoToAsync
Routing.RegisterRoute(nameof(MovieDetailPage), typeof(MovieDetailPage));

// Call site
await Facade.Navigation.GoToAsync(nameof(MovieDetailPage), new MovieDetailParameters(movie.Id, movie.Title));
```

### `isGoBack` Pattern

`ShellNavigationService.GoBackAsync` injects `"__isBack": true` into the query before popping. `BaseViewModel.ApplyNavigationParameters` (via `BasePage.ApplyQueryAttributes`) extracts this flag and passes it as `isGoBack` to `InitializeFromQueryAsync`, allowing ViewModels to skip re-fetching data when returning from a child page.

**Modal dismiss does not trigger `isGoBack`.** `DismissModalAsync` calls `Navigation.PopModalAsync`, which bypasses Shell routing entirely — `ApplyQueryAttributes` is never called on the page underneath. If `BrowsePage` needs to refresh after the Filter modal is dismissed, it must do so in `OnAppearingAsync`, not `InitializeFromQueryAsync`.

---

## BaseViewModelFacade

Aggregates shared services so ViewModels have a single constructor dependency instead of many.

```csharp
public class BaseViewModelFacade(INavigationService navigation, ILogger<BaseViewModelFacade> logger)
{
    public INavigationService Navigation { get; } = navigation;
    public ILogger Logger { get; } = logger;
}
```

Registered as **Singleton** in DI.

---

## BaseViewModel\<T\>

`T` is the concrete ViewModel type. Implements `ObservableValidator` (CommunityToolkit.Mvvm), `INavigationParameterReceiver` (Core-owned, see below), and `IDisposable`.

**`INavigationParameterReceiver`** is defined in `MauiNavigation.Core.Navigation` — a Core-owned substitute for MAUI's `IQueryAttributable`, which lives in `Microsoft.Maui.Controls` and cannot be referenced from Core:

```csharp
// In Core — no MAUI reference required
public interface INavigationParameterReceiver
{
    void ApplyNavigationParameters(IDictionary<string, object> query);
}
```

`BasePage<TViewModel>` (in the MAUI project) bridges the two: it implements the MAUI `IQueryAttributable` and forwards to the ViewModel:

```csharp
// In BasePage (MAUI project) — the only place that touches IQueryAttributable
public void ApplyQueryAttributes(IDictionary<string, object> query)
    => ViewModel.ApplyNavigationParameters(query);
```

This keeps the Core/MAUI boundary clean: Shell calls `IQueryAttributable.ApplyQueryAttributes` on the page, the page delegates to the ViewModel via the Core interface.

**Responsibilities:**
- Lifecycle hooks: `OnAppearingAsync(CancellationToken)` / `OnDisappearingAsync()` — `protected virtual`, override in subclasses
- Lifecycle wrappers: `OnAppearingInternal()` / `OnDisappearingInternal()` — `public`, called by `BasePage`. These manage the CTS (create/cancel/dispose) and then delegate to the virtual `Async` methods. Subclasses never call these directly.
  - Call chain: `BasePage.OnAppearing` → `BaseViewModel.OnAppearingInternal` (manages CTS) → `virtual OnAppearingAsync(ct)`
  - Call chain: `BasePage.OnDisappearing` → `BaseViewModel.OnDisappearingInternal` (cancels CTS) → `virtual OnDisappearingAsync()`
- CTS management: lifecycle `CancellationTokenSource` created inside `OnAppearingInternal`, cancelled in `OnDisappearingInternal`. Re-entrant: before disposing the CTS, `OnDisappearingInternal` checks `ReferenceEquals(this.lifecycleCts, cts)` — comparing the field's current value against the local reference captured before cancellation — to guard against a race where a rapid re-appear already assigned a new CTS to the field.
- `ApplyNavigationParameters`: implements `INavigationParameterReceiver`. Extracts `__isBack`, fires `InitializeFromQueryAsync` via `SafeFireAndForget` with `IsBusy = true`
- `IsBusy`: ref-counted via `busyCount` (`Interlocked`) — nested calls don't flip it off prematurely. `ExecuteSafeAsync` wraps the decrement in a `finally` block, guaranteeing the counter is always decremented even if the action throws.
- `SafeFireAndForget` / `ExecuteSafeAsync`: all exceptions caught and logged via `Facade.Logger`, `OperationCanceledException` silently swallowed
- `Dispose`: cancels CTS, idempotent, thread-safe via `isDisposed` flag

---

## BasePage\<TViewModel\>

Thin MAUI-side glue. Lives in the MAUI project (can reference `Microsoft.Maui.Controls`).

**Responsibilities:**
- Constructor: takes `TViewModel`, sets `BindingContext`, hides Shell nav bar by default
- `OnAppearing` → `ViewModel.OnAppearingInternal()`
- `OnDisappearing` → `ViewModel.OnDisappearingInternal()`
- `IQueryAttributable.ApplyQueryAttributes` → `ViewModel.ApplyNavigationParameters(query)` — bridges MAUI's Shell routing to the Core interface
- `Dispose` → `ViewModel.Dispose()` (if `IDisposable`)

---

## AppShell — TabBar & Route Registration

```
AppShell
└── TabBar
    ├── Tab "Browse"     → BrowsePage     (tab root, owns its own nav stack)
    └── Tab "Favorites"  → FavoritesPage  (tab root, owns its own nav stack)

Registered routes (Shell routing only — no tab chrome):
  nameof(MovieDetailPage) → MovieDetailPage   (pushed from Browse tab via GoToAsync)

NOT registered with Routing (modal, bypasses Shell routing):
  FilterPage — registered with ShellNavigationService.RegisterModal instead
```

`AppShell` receives `INavigationService` via constructor injection so it can register modal routes alongside Shell routes:

```csharp
public AppShell(INavigationService navigationService)
{
    InitializeComponent();
    // Shell push routes
    Routing.RegisterRoute(nameof(MovieDetailPage), typeof(MovieDetailPage));
    // Modal routes — registered with NavigationService, not Shell.Routing
    navigationService.RegisterModal(nameof(FilterPage), typeof(FilterPage));
}
```

---

## ShellNavigationService

Lives in the MAUI project. The only place in the solution that touches `Shell.Current`.

- `GoToAsync` → `Shell.Current.GoToAsync(route, animated, shellParams)`
- `GoBackAsync` → injects `__isBack`, then `Shell.Current.GoToAsync("..", animated, shellParams)`
- `PresentModalAsync` / `PresentModalAsync<TParams>` → resolves page type from an internal modal registry (see below), then gets the instance from `IServiceProvider`, delivers params, and calls `Shell.Current.Navigation.PushModalAsync`.

**Modal registry** — `ShellNavigationService` maintains a `Dictionary<string, Type>` for modal page types, populated by a `RegisterModal(string route, Type pageType)` method. This mirrors `Routing.RegisterRoute` and is called from `AppShell.xaml.cs`:

  ```csharp
  // AppShell.xaml.cs
  Routing.RegisterRoute(nameof(MovieDetailPage), typeof(MovieDetailPage));      // Shell push
  _navigationService.RegisterModal(nameof(FilterPage), typeof(FilterPage));     // modal
  ```

  At runtime, `PresentModalAsync` looks up the type by route, resolves it from `IServiceProvider`, and — since `PushModalAsync` bypasses Shell routing — delivers parameters by calling `ApplyNavigationParameters` directly on the ViewModel's `INavigationParameterReceiver` interface:

  ```csharp
  var pageType = _modalRegistry[route];
  var page = (ContentPage)_serviceProvider.GetRequiredService(pageType);
  if (parameters != null && page.BindingContext is INavigationParameterReceiver receiver)
      receiver.ApplyNavigationParameters(new Dictionary<string, object> { ["__params"] = parameters });
  await Shell.Current.Navigation.PushModalAsync(page, animated);
  ```

  This keeps parameter delivery consistent — ViewModels always receive params via `ApplyNavigationParameters` regardless of push mechanism.
- `DismissModalAsync` → `Shell.Current.Navigation.PopModalAsync`

Service locator is **contained entirely here** — ViewModels never touch `IServiceProvider` directly.

---

## DI Wiring (`MauiProgram.cs`)

Since `AppShell` has constructor parameters it must be resolved from DI. `App.xaml.cs` sets `MainPage` via the service provider rather than `new AppShell()`:

```csharp
// App.xaml.cs
public App(IServiceProvider services)
{
    InitializeComponent();
    MainPage = services.GetRequiredService<AppShell>();
}
```

```csharp
// MauiProgram.cs
// Infrastructure
builder.Services.AddSingleton<INavigationService, ShellNavigationService>();
builder.Services.AddSingleton<BaseViewModelFacade>();
builder.Services.AddSingleton<AppShell>();

// ViewModels — Transient (new instance per navigation)
builder.Services.AddTransient<BrowseViewModel>();
builder.Services.AddTransient<FavoritesViewModel>();
builder.Services.AddTransient<MovieDetailViewModel>();
builder.Services.AddTransient<FilterViewModel>();

// Pages — Transient (lifetime matches ViewModel)
builder.Services.AddTransient<BrowsePage>();
builder.Services.AddTransient<FavoritesPage>();
builder.Services.AddTransient<MovieDetailPage>();
builder.Services.AddTransient<FilterPage>();
```

---

## Article Structure (`NAVIGATION_GUIDE.md`)

1. Why Shell navigation + the Core/App split
2. Project setup (two projects, NuGet deps)
3. `INavigationService` contract + strongly-typed parameters
4. `BaseViewModelFacade` — the service aggregator pattern
5. `BaseViewModel<T>` — lifecycle, cancellation, busy state, `isGoBack`
6. `BasePage<TViewModel>` — the MAUI-side glue
7. AppShell — TabBar, route registration with `nameof()`
8. `ShellNavigationService` — full implementation walkthrough
9. Wiring it all up in `MauiProgram.cs`
10. Real-world walk-through: Browse → Detail → Back, Browse → Filter modal → Dismiss
11. Gotchas & tips (Transient vs Singleton, modal vs push, `isGoBack` pattern, modal dismiss does not trigger `ApplyQueryAttributes` — use `OnAppearingAsync` for post-dismiss refresh)

---

## NuGet Dependencies

| Package | Project | Purpose |
|---|---|---|
| `CommunityToolkit.Mvvm` | Core | `ObservableValidator`, `[ObservableProperty]` source gen |
| `Microsoft.Extensions.Logging.Abstractions` | Core | `ILogger` in facade |
| `Microsoft.Maui.Controls` | App only | Shell, ContentPage, etc. |
| `Microsoft.Extensions.Logging.Debug` | App only | Debug logging |
