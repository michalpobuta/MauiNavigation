# MAUI Navigation Guide Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a production-grade .NET MAUI navigation example app (Movies domain) demonstrating Shell TabBar, push navigation with typed parameters, modal presentation, and lifecycle management, accompanied by a `NAVIGATION_GUIDE.md` article.

**Architecture:** Two-project solution — `MauiNavigation.Core` (net10.0 class library, zero MAUI refs) owns interfaces, base classes, and ViewModels; `MauiNavigation` (MAUI app) owns Shell infrastructure, pages, and DI wiring. `ShellNavigationService` is the single place that touches `Shell.Current`. `INavigationParameterReceiver` is a Core-owned interface that replaces MAUI's `IQueryAttributable` at the boundary.

**Tech Stack:** .NET 10, .NET MAUI Shell, CommunityToolkit.Mvvm 8.x, Microsoft.Extensions.Logging.Abstractions, xUnit, NSubstitute.

---

## File Map

**Create — MauiNavigation.Core/**
- `MauiNavigation.Core.csproj`
- `Navigation/INavigationService.cs`
- `Navigation/INavigationParameterReceiver.cs`
- `Navigation/Routes.cs` — string constants matching page names (Core can't use `nameof(PageType)`)
- `Navigation/Parameters/MovieDetailParameters.cs`
- `Navigation/Parameters/FilterParameters.cs`
- `Base/BaseViewModelFacade.cs`
- `Base/BaseViewModel.cs`
- `Models/Movie.cs`
- `ViewModels/BrowseViewModel.cs`
- `ViewModels/FavoritesViewModel.cs`
- `ViewModels/MovieDetailViewModel.cs`
- `ViewModels/FilterViewModel.cs`

**Create — MauiNavigation.Tests/**
- `MauiNavigation.Tests.csproj`
- `Base/BaseViewModelTests.cs`
- `ViewModels/BrowseViewModelTests.cs`
- `ViewModels/MovieDetailViewModelTests.cs`

**Create — MauiNavigation/**
- `Infrastructure/ShellNavigationService.cs`
- `Base/BasePage.cs`
- `Pages/Browse/BrowsePage.xaml` + `.cs`
- `Pages/Favorites/FavoritesPage.xaml` + `.cs`
- `Pages/MovieDetail/MovieDetailPage.xaml` + `.cs`
- `Pages/Filter/FilterPage.xaml` + `.cs`

**Modify — MauiNavigation/**
- `MauiNavigation.csproj` — add Core project reference
- `MauiProgram.cs` — full DI wiring
- `App.xaml.cs` — resolve `AppShell` from DI instead of `new AppShell()`
- `AppShell.xaml` — TabBar with Browse + Favorites tabs
- `AppShell.xaml.cs` — constructor injection, route registration

**Delete — MauiNavigation/**
- `MainPage.xaml` + `MainPage.xaml.cs`

**Create at repo root:**
- `NAVIGATION_GUIDE.md`

---

### Task 1: Add MauiNavigation.Core project to solution

**Files:**
- Create: `MauiNavigation.Core/MauiNavigation.Core.csproj`
- Modify: `MauiNavigation.sln` (via `dotnet sln add`)
- Modify: `MauiNavigation/MauiNavigation.csproj` (via `dotnet add reference`)

- [ ] **Step 1: Create Core project directory and csproj**

Create `MauiNavigation.Core/MauiNavigation.Core.csproj`:
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

- [ ] **Step 2: Add Core project to solution and reference it from the MAUI app**

Run from the repo root (`/Users/mipb/RiderProjects/MauiNavigation`):
```bash
dotnet sln MauiNavigation.sln add MauiNavigation.Core/MauiNavigation.Core.csproj
dotnet add MauiNavigation/MauiNavigation.csproj reference MauiNavigation.Core/MauiNavigation.Core.csproj
```

- [ ] **Step 3: Verify Core builds**

```bash
dotnet build MauiNavigation.Core/MauiNavigation.Core.csproj
```

Expected: `Build succeeded.`

---

### Task 2: Navigation contracts and parameter records in Core

**Files:**
- Create: `MauiNavigation.Core/Navigation/INavigationService.cs`
- Create: `MauiNavigation.Core/Navigation/INavigationParameterReceiver.cs`
- Create: `MauiNavigation.Core/Navigation/Routes.cs`
- Create: `MauiNavigation.Core/Navigation/Parameters/MovieDetailParameters.cs`
- Create: `MauiNavigation.Core/Navigation/Parameters/FilterParameters.cs`

- [ ] **Step 1: Create INavigationService**

`MauiNavigation.Core/Navigation/INavigationService.cs`:
```csharp
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

- [ ] **Step 2: Create INavigationParameterReceiver**

This is a Core-owned substitute for MAUI's `IQueryAttributable` (which lives in `Microsoft.Maui.Controls` and cannot be referenced from Core). `BasePage` bridges the two.

`MauiNavigation.Core/Navigation/INavigationParameterReceiver.cs`:
```csharp
namespace MauiNavigation.Core.Navigation;

public interface INavigationParameterReceiver
{
    void ApplyNavigationParameters(IDictionary<string, object> query);
}
```

- [ ] **Step 3: Create Routes constants**

ViewModels in Core cannot use `nameof(MovieDetailPage)` since they can't reference MAUI page types. `Routes` provides string constants that match the registered page class names. Registration in `AppShell` still uses `nameof()` — the two must stay in sync.

`MauiNavigation.Core/Navigation/Routes.cs`:
```csharp
namespace MauiNavigation.Core.Navigation;

/// <summary>
/// Route names matching the MAUI page class names.
/// AppShell registers these with Routing.RegisterRoute(nameof(PageType), ...)
/// and navigationService.RegisterModal(nameof(PageType), ...) — those strings
/// must equal the constants here.
/// </summary>
public static class Routes
{
    public const string MovieDetail = "MovieDetailPage";
    public const string Filter = "FilterPage";
}
```

- [ ] **Step 4: Create parameter records**

`MauiNavigation.Core/Navigation/Parameters/MovieDetailParameters.cs`:
```csharp
namespace MauiNavigation.Core.Navigation.Parameters;

public record MovieDetailParameters(int MovieId, string Title);
```

`MauiNavigation.Core/Navigation/Parameters/FilterParameters.cs`:
```csharp
namespace MauiNavigation.Core.Navigation.Parameters;

public record FilterParameters(string? Genre, int? MinYear);
```

- [ ] **Step 5: Verify build**

```bash
dotnet build MauiNavigation.Core/MauiNavigation.Core.csproj
```

Expected: `Build succeeded.`

---

### Task 3: Create test project

**Files:**
- Create: `MauiNavigation.Tests/MauiNavigation.Tests.csproj`
- Create: `MauiNavigation.Tests/Smoke/SmokeTest.cs`

- [ ] **Step 1: Create test project csproj**

`MauiNavigation.Tests/MauiNavigation.Tests.csproj`:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <RootNamespace>MauiNavigation.Tests</RootNamespace>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <IsPackable>false</IsPackable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.13.0" />
    <PackageReference Include="xunit" Version="2.9.3" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.8.2">
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
      <PrivateAssets>all</PrivateAssets>
    </PackageReference>
    <PackageReference Include="NSubstitute" Version="5.3.0" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="../MauiNavigation.Core/MauiNavigation.Core.csproj" />
  </ItemGroup>
</Project>
```

- [ ] **Step 2: Add test project to solution**

```bash
dotnet sln MauiNavigation.sln add MauiNavigation.Tests/MauiNavigation.Tests.csproj
```

- [ ] **Step 3: Write smoke test**

`MauiNavigation.Tests/Smoke/SmokeTest.cs`:
```csharp
namespace MauiNavigation.Tests.Smoke;

public class SmokeTest
{
    [Fact]
    public void True_IsTrue() => Assert.True(true);
}
```

- [ ] **Step 4: Run tests**

```bash
dotnet test MauiNavigation.Tests/MauiNavigation.Tests.csproj
```

Expected: `Passed! - 1: 1`

---

### Task 4: BaseViewModelFacade and BaseViewModel (TDD)

**Files:**
- Create: `MauiNavigation.Tests/Base/BaseViewModelTests.cs`
- Create: `MauiNavigation.Core/Base/BaseViewModelFacade.cs`
- Create: `MauiNavigation.Core/Base/BaseViewModel.cs`

- [ ] **Step 1: Write failing tests**

`MauiNavigation.Tests/Base/BaseViewModelTests.cs`:
```csharp
using MauiNavigation.Core.Base;
using MauiNavigation.Core.Navigation;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace MauiNavigation.Tests.Base;

// Concrete testable ViewModel — file-scoped so it doesn't pollute test namespace
file sealed class TestViewModel(BaseViewModelFacade facade) : BaseViewModel<TestViewModel>(facade)
{
    public bool AppearingCalled { get; private set; }
    public bool DisappearingCalled { get; private set; }
    public CancellationToken LastToken { get; private set; }

    private readonly TaskCompletionSource<(IDictionary<string, object> Query, bool IsGoBack)> _initTcs = new();
    public Task<(IDictionary<string, object> Query, bool IsGoBack)> InitializeTask => _initTcs.Task;

    protected override Task OnAppearingAsync(CancellationToken ct)
    {
        AppearingCalled = true;
        LastToken = ct;
        return Task.CompletedTask;
    }

    protected override Task OnDisappearingAsync()
    {
        DisappearingCalled = true;
        return Task.CompletedTask;
    }

    public override Task InitializeFromQueryAsync(IDictionary<string, object> query, bool isGoBack, CancellationToken ct)
    {
        _initTcs.TrySetResult((query, isGoBack));
        return Task.CompletedTask;
    }
}

file static class Facades
{
    public static BaseViewModelFacade Create() => new(
        Substitute.For<INavigationService>(),
        NullLogger<BaseViewModelFacade>.Instance);
}

public class BaseViewModelTests
{
    [Fact]
    public async Task OnAppearingInternal_CallsOnAppearingAsync()
    {
        var vm = new TestViewModel(Facades.Create());
        await vm.OnAppearingInternal();
        Assert.True(vm.AppearingCalled);
    }

    [Fact]
    public async Task OnDisappearingInternal_CallsOnDisappearingAsync()
    {
        var vm = new TestViewModel(Facades.Create());
        await vm.OnAppearingInternal();
        await vm.OnDisappearingInternal();
        Assert.True(vm.DisappearingCalled);
    }

    [Fact]
    public async Task OnDisappearingInternal_CancelsLifecycleToken()
    {
        var vm = new TestViewModel(Facades.Create());
        await vm.OnAppearingInternal();
        var token = vm.LastToken;
        await vm.OnDisappearingInternal();
        Assert.True(token.IsCancellationRequested);
    }

    [Fact]
    public async Task ApplyNavigationParameters_PassesIsGoBackTrue_WhenIsBackKeyPresent()
    {
        var vm = new TestViewModel(Facades.Create());
        await vm.OnAppearingInternal();
        var query = new Dictionary<string, object> { ["__isBack"] = true };
        vm.ApplyNavigationParameters(query);
        var (_, isGoBack) = await vm.InitializeTask.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.True(isGoBack);
    }

    [Fact]
    public async Task ApplyNavigationParameters_PassesIsGoBackFalse_WhenIsBackKeyAbsent()
    {
        var vm = new TestViewModel(Facades.Create());
        await vm.OnAppearingInternal();
        var query = new Dictionary<string, object> { ["__params"] = "anything" };
        vm.ApplyNavigationParameters(query);
        var (_, isGoBack) = await vm.InitializeTask.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.False(isGoBack);
    }

    [Fact]
    public async Task Dispose_CancelsLifecycleToken()
    {
        var vm = new TestViewModel(Facades.Create());
        await vm.OnAppearingInternal();
        var token = vm.LastToken;
        vm.Dispose();
        Assert.True(token.IsCancellationRequested);
    }

    [Fact]
    public void Dispose_IsIdempotent()
    {
        var vm = new TestViewModel(Facades.Create());
        vm.Dispose();
        var ex = Record.Exception(() => vm.Dispose());
        Assert.Null(ex);
    }
}
```

- [ ] **Step 2: Run tests — expect build failure**

```bash
dotnet test MauiNavigation.Tests/MauiNavigation.Tests.csproj
```

Expected: Build error — `BaseViewModel`, `BaseViewModelFacade` not found.

- [ ] **Step 3: Implement BaseViewModelFacade**

`MauiNavigation.Core/Base/BaseViewModelFacade.cs`:
```csharp
using MauiNavigation.Core.Navigation;
using Microsoft.Extensions.Logging;

namespace MauiNavigation.Core.Base;

public class BaseViewModelFacade(INavigationService navigation, ILogger<BaseViewModelFacade> logger)
{
    public INavigationService Navigation { get; } = navigation;
    public ILogger Logger { get; } = logger;
}
```

- [ ] **Step 4: Implement BaseViewModel**

`MauiNavigation.Core/Base/BaseViewModel.cs`:
```csharp
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
        // Remove returns true if the key existed — that IS the isGoBack flag.
        var isGoBack = query.Remove("__isBack");
        SafeFireAndForget(ct => InitializeFromQueryAsync(query, isGoBack, ct), showLoader: true);
    }

    /// <summary>
    /// Fire-and-forget wrapper. Catches all exceptions and logs them.
    /// OperationCanceledException is silently swallowed.
    /// </summary>
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
            // Decrement is in finally — always runs even if action throws.
            // Ref-counting means nested SafeFireAndForget calls don't flip IsBusy off prematurely.
            if (showLoader && Interlocked.Decrement(ref _busyCount) <= 0)
                IsBusy = false;
        }
    }
}
```

- [ ] **Step 5: Run tests — expect pass**

```bash
dotnet test MauiNavigation.Tests/MauiNavigation.Tests.csproj
```

Expected: All 7 tests pass.

---

### Task 5: Movie model, BrowseViewModel, and FavoritesViewModel (TDD)

**Files:**
- Create: `MauiNavigation.Tests/ViewModels/BrowseViewModelTests.cs`
- Create: `MauiNavigation.Core/Models/Movie.cs`
- Create: `MauiNavigation.Core/ViewModels/BrowseViewModel.cs`
- Create: `MauiNavigation.Core/ViewModels/FavoritesViewModel.cs`

- [ ] **Step 1: Write failing tests for BrowseViewModel**

`MauiNavigation.Tests/ViewModels/BrowseViewModelTests.cs`:
```csharp
using CommunityToolkit.Mvvm.Input;
using MauiNavigation.Core.Base;
using MauiNavigation.Core.Models;
using MauiNavigation.Core.Navigation;
using MauiNavigation.Core.Navigation.Parameters;
using MauiNavigation.Core.ViewModels;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace MauiNavigation.Tests.ViewModels;

public class BrowseViewModelTests
{
    private static BaseViewModelFacade CreateFacade(INavigationService? nav = null) => new(
        nav ?? Substitute.For<INavigationService>(),
        NullLogger<BaseViewModelFacade>.Instance);

    [Fact]
    public async Task OnAppearingAsync_PopulatesMovies()
    {
        var vm = new BrowseViewModel(CreateFacade());
        await vm.OnAppearingInternal();
        Assert.NotEmpty(vm.Movies);
    }

    [Fact]
    public async Task OnAppearingAsync_CalledTwice_DoesNotDuplicateMovies()
    {
        var vm = new BrowseViewModel(CreateFacade());
        await vm.OnAppearingInternal();
        var count = vm.Movies.Count;
        await vm.OnDisappearingInternal();
        await vm.OnAppearingInternal();
        Assert.Equal(count, vm.Movies.Count);
    }

    [Fact]
    public async Task SelectMovieCommand_NavigatesToMovieDetail_WithCorrectParameters()
    {
        var nav = Substitute.For<INavigationService>();
        var vm = new BrowseViewModel(CreateFacade(nav));
        await vm.OnAppearingInternal();
        var movie = vm.Movies.First();

        await ((AsyncRelayCommand<Movie>)vm.SelectMovieCommand).ExecuteAsync(movie);

        await nav.Received(1).GoToAsync(
            Routes.MovieDetail,
            Arg.Is<MovieDetailParameters>(p => p.MovieId == movie.Id && p.Title == movie.Title),
            Arg.Any<bool>());
    }

    [Fact]
    public async Task OpenFilterCommand_PresentsFilterModal()
    {
        var nav = Substitute.For<INavigationService>();
        var vm = new BrowseViewModel(CreateFacade(nav));
        await vm.OnAppearingInternal();

        await ((AsyncRelayCommand)vm.OpenFilterCommand).ExecuteAsync(null);

        await nav.Received(1).PresentModalAsync(Routes.Filter, Arg.Any<bool>());
    }
}
```

- [ ] **Step 2: Run tests — expect build failure**

```bash
dotnet test MauiNavigation.Tests/MauiNavigation.Tests.csproj
```

Expected: Build error — `Movie`, `BrowseViewModel` not found.

- [ ] **Step 3: Create Movie model**

`MauiNavigation.Core/Models/Movie.cs`:
```csharp
namespace MauiNavigation.Core.Models;

public record Movie(int Id, string Title, string Genre, int Year, string Description);
```

- [ ] **Step 4: Implement BrowseViewModel**

`MauiNavigation.Core/ViewModels/BrowseViewModel.cs`:
```csharp
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using MauiNavigation.Core.Base;
using MauiNavigation.Core.Models;
using MauiNavigation.Core.Navigation;
using MauiNavigation.Core.Navigation.Parameters;

namespace MauiNavigation.Core.ViewModels;

public partial class BrowseViewModel(BaseViewModelFacade facade) : BaseViewModel<BrowseViewModel>(facade)
{
    private static readonly Movie[] SampleMovies =
    [
        new(1, "The Shawshank Redemption", "Drama", 1994, "Two imprisoned men bond over years, finding solace and eventual redemption through acts of common decency."),
        new(2, "The Godfather", "Crime", 1972, "The aging patriarch of an organized crime dynasty transfers control of his empire to his reluctant son."),
        new(3, "Inception", "Sci-Fi", 2010, "A thief who steals corporate secrets through dream-sharing technology is given the task of planting an idea."),
        new(4, "Interstellar", "Sci-Fi", 2014, "A team of explorers travel through a wormhole in space in an attempt to ensure humanity's survival."),
        new(5, "Pulp Fiction", "Crime", 1994, "The lives of two mob hitmen, a boxer, a gangster and his wife intertwine in tales of violence and redemption."),
    ];

    public ObservableCollection<Movie> Movies { get; } = [];

    protected override Task OnAppearingAsync(CancellationToken cancellationToken)
    {
        // Guard against re-loading on every appear (e.g. returning from MovieDetail).
        // In a real app this would be an async service call and you'd use isGoBack
        // in InitializeFromQueryAsync to decide whether to skip the fetch.
        if (Movies.Count == 0)
        {
            foreach (var movie in SampleMovies)
                Movies.Add(movie);
        }

        return Task.CompletedTask;
    }

    [RelayCommand]
    private Task SelectMovie(Movie movie) =>
        Facade.Navigation.GoToAsync(Routes.MovieDetail, new MovieDetailParameters(movie.Id, movie.Title));

    [RelayCommand]
    private Task OpenFilter() =>
        Facade.Navigation.PresentModalAsync(Routes.Filter);
}
```

- [ ] **Step 5: Implement FavoritesViewModel**

`MauiNavigation.Core/ViewModels/FavoritesViewModel.cs`:
```csharp
using System.Collections.ObjectModel;
using MauiNavigation.Core.Base;
using MauiNavigation.Core.Models;

namespace MauiNavigation.Core.ViewModels;

public partial class FavoritesViewModel(BaseViewModelFacade facade) : BaseViewModel<FavoritesViewModel>(facade)
{
    // In a real app, favorites would be persisted and loaded here.
    // For this demo the list stays empty to keep focus on navigation patterns.
    public ObservableCollection<Movie> Favorites { get; } = [];
}
```

- [ ] **Step 6: Run tests — expect pass**

```bash
dotnet test MauiNavigation.Tests/MauiNavigation.Tests.csproj
```

Expected: All tests pass.

---

### Task 6: MovieDetailViewModel and FilterViewModel (TDD)

**Files:**
- Create: `MauiNavigation.Tests/ViewModels/MovieDetailViewModelTests.cs`
- Create: `MauiNavigation.Core/ViewModels/MovieDetailViewModel.cs`
- Create: `MauiNavigation.Core/ViewModels/FilterViewModel.cs`

- [ ] **Step 1: Write failing tests**

`MauiNavigation.Tests/ViewModels/MovieDetailViewModelTests.cs`:
```csharp
using MauiNavigation.Core.Base;
using MauiNavigation.Core.Navigation;
using MauiNavigation.Core.Navigation.Parameters;
using MauiNavigation.Core.ViewModels;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace MauiNavigation.Tests.ViewModels;

public class MovieDetailViewModelTests
{
    private static BaseViewModelFacade CreateFacade() => new(
        Substitute.For<INavigationService>(),
        NullLogger<BaseViewModelFacade>.Instance);

    [Fact]
    public async Task InitializeFromQueryAsync_SetsTitle_FromMovieDetailParameters()
    {
        var vm = new MovieDetailViewModel(CreateFacade());
        var query = new Dictionary<string, object>
        {
            ["__params"] = new MovieDetailParameters(1, "Inception")
        };

        await vm.InitializeFromQueryAsync(query, isGoBack: false, CancellationToken.None);

        Assert.Equal("Inception", vm.Title);
    }

    [Fact]
    public async Task InitializeFromQueryAsync_DoesNotOverwrite_WhenIsGoBack()
    {
        var vm = new MovieDetailViewModel(CreateFacade());
        vm.Title = "Already Set";
        var query = new Dictionary<string, object>
        {
            ["__params"] = new MovieDetailParameters(1, "Inception")
        };

        await vm.InitializeFromQueryAsync(query, isGoBack: true, CancellationToken.None);

        Assert.Equal("Already Set", vm.Title);
    }
}
```

- [ ] **Step 2: Run tests — expect build failure**

```bash
dotnet test MauiNavigation.Tests/MauiNavigation.Tests.csproj
```

Expected: Build error — `MovieDetailViewModel` not found.

- [ ] **Step 3: Implement MovieDetailViewModel**

`MauiNavigation.Core/ViewModels/MovieDetailViewModel.cs`:
```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MauiNavigation.Core.Base;
using MauiNavigation.Core.Navigation.Parameters;

namespace MauiNavigation.Core.ViewModels;

public partial class MovieDetailViewModel(BaseViewModelFacade facade) : BaseViewModel<MovieDetailViewModel>(facade)
{
    [ObservableProperty]
    private string _title = string.Empty;

    [ObservableProperty]
    private int _movieId;

    public override Task InitializeFromQueryAsync(IDictionary<string, object> query, bool isGoBack, CancellationToken ct)
    {
        // Skip re-loading when returning back to this page from a child.
        if (isGoBack) return Task.CompletedTask;

        if (query.TryGetValue("__params", out var p) && p is MovieDetailParameters parms)
        {
            MovieId = parms.MovieId;
            Title = parms.Title;
            // In a real app: await movieService.GetByIdAsync(parms.MovieId, ct)
        }

        return Task.CompletedTask;
    }

    [RelayCommand]
    private Task GoBack() => Facade.Navigation.GoBackAsync();
}
```

- [ ] **Step 4: Implement FilterViewModel**

`MauiNavigation.Core/ViewModels/FilterViewModel.cs`:
```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MauiNavigation.Core.Base;
using MauiNavigation.Core.Navigation.Parameters;

namespace MauiNavigation.Core.ViewModels;

public partial class FilterViewModel(BaseViewModelFacade facade) : BaseViewModel<FilterViewModel>(facade)
{
    [ObservableProperty]
    private string? _genre;

    [ObservableProperty]
    private string? _minYear; // String for easy Entry binding; parse to int when applying

    public override Task InitializeFromQueryAsync(IDictionary<string, object> query, bool isGoBack, CancellationToken ct)
    {
        if (query.TryGetValue("__params", out var p) && p is FilterParameters parms)
        {
            Genre = parms.Genre;
            MinYear = parms.MinYear?.ToString();
        }

        return Task.CompletedTask;
    }

    [RelayCommand]
    private Task Apply()
    {
        // In a real app: save filter state to a service before dismissing
        // so BrowseViewModel can read it in OnAppearingAsync.
        return Facade.Navigation.DismissModalAsync();
    }

    [RelayCommand]
    private Task Dismiss() => Facade.Navigation.DismissModalAsync();
}
```

- [ ] **Step 5: Run all tests — expect pass**

```bash
dotnet test MauiNavigation.Tests/MauiNavigation.Tests.csproj
```

Expected: All tests pass.

---

### Task 7: ShellNavigationService

**Files:**
- Create: `MauiNavigation/Infrastructure/ShellNavigationService.cs`

Note: `ShellNavigationService` depends on `Shell.Current` which is only available at runtime on a device/simulator. There are no unit tests for this class — it is the thin infrastructure layer that connects Core's `INavigationService` contract to MAUI.

- [ ] **Step 1: Implement ShellNavigationService**

`MauiNavigation/Infrastructure/ShellNavigationService.cs`:
```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Controls;
using MauiNavigation.Core.Navigation;

namespace MauiNavigation.Infrastructure;

public class ShellNavigationService(IServiceProvider serviceProvider) : INavigationService
{
    private readonly Dictionary<string, Type> _modalRegistry = new();

    /// <summary>
    /// Registers a page type as a modal route.
    /// Modals bypass Shell routing — they are resolved from DI and pushed via PushModalAsync.
    /// Call this from AppShell alongside Routing.RegisterRoute for push routes.
    /// </summary>
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

- [ ] **Step 2: Verify MAUI app compiles (Core already verified)**

```bash
dotnet build MauiNavigation/MauiNavigation.csproj -f net10.0-android
```

Expected: `Build succeeded.` (or warnings only — no errors)

---

### Task 8: BasePage and App.xaml.cs

**Files:**
- Create: `MauiNavigation/Base/BasePage.cs`
- Modify: `MauiNavigation/App.xaml.cs`

- [ ] **Step 1: Implement BasePage**

`MauiNavigation/Base/BasePage.cs`:
```csharp
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

- [ ] **Step 2: Update App.xaml.cs to resolve AppShell from DI**

Read the current `App.xaml.cs` first, then replace the constructor so `AppShell` is resolved from the DI container (required because `AppShell` now has constructor parameters):

`MauiNavigation/App.xaml.cs`:
```csharp
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

- [ ] **Step 3: Verify build**

```bash
dotnet build MauiNavigation/MauiNavigation.csproj -f net10.0-android
```

Expected: `Build succeeded.`

---

### Task 9: AppShell + DI wiring in MauiProgram

**Files:**
- Modify: `MauiNavigation/AppShell.xaml`
- Modify: `MauiNavigation/AppShell.xaml.cs`
- Modify: `MauiNavigation/MauiProgram.cs`

`AppShell` needs the Browse and Favorites page types. Those don't exist yet — this task creates the shell structure so pages can be slotted in Task 10.

- [ ] **Step 1: Update AppShell.xaml to TabBar layout**

`MauiNavigation/AppShell.xaml`:
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

- [ ] **Step 2: Update AppShell.xaml.cs — constructor injection + route registration**

`MauiNavigation/AppShell.xaml.cs`:
```csharp
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

- [ ] **Step 3: Add CommunityToolkit.Maui (needed for InvertedBoolConverter in BrowsePage)**

```bash
dotnet add MauiNavigation/MauiNavigation.csproj package CommunityToolkit.Maui
```

- [ ] **Step 4: Update MauiProgram.cs with full DI wiring**

`MauiNavigation/MauiProgram.cs`:
```csharp
using Microsoft.Extensions.Logging;
using MauiNavigation.Base;
using MauiNavigation.Core.Base;
using MauiNavigation.Core.Navigation;
using MauiNavigation.Core.ViewModels;
using MauiNavigation.Infrastructure;
using MauiNavigation.Pages.Browse;
using MauiNavigation.Pages.Favorites;
using MauiNavigation.Pages.MovieDetail;
using MauiNavigation.Pages.Filter;

namespace MauiNavigation;

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

- [ ] **Step 4: Verify build (pages don't exist yet — expect errors referencing Browse/Favorites/etc.)**

```bash
dotnet build MauiNavigation/MauiNavigation.csproj -f net10.0-android 2>&1 | head -20
```

Expected: Build errors only about missing page types (`BrowsePage`, `FavoritesPage`, etc.) — not about infrastructure.

---

### Task 10: Browse and Favorites pages

**Files:**
- Create: `MauiNavigation/Pages/Browse/BrowsePage.xaml` + `.cs`
- Create: `MauiNavigation/Pages/Favorites/FavoritesPage.xaml` + `.cs`

These are tab root pages — no nav bar, no back button.

- [ ] **Step 1: Create BrowsePage XAML**

`MauiNavigation/Pages/Browse/BrowsePage.xaml`:
```xml
<?xml version="1.0" encoding="utf-8" ?>
<base:BasePage
    x:TypeArguments="viewmodels:BrowseViewModel"
    x:Class="MauiNavigation.Pages.Browse.BrowsePage"
    xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
    xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
    xmlns:base="clr-namespace:MauiNavigation.Base"
    xmlns:viewmodels="clr-namespace:MauiNavigation.Core.ViewModels;assembly=MauiNavigation.Core"
    xmlns:models="clr-namespace:MauiNavigation.Core.Models;assembly=MauiNavigation.Core">

    <Grid RowDefinitions="Auto,*">

        <!-- Toolbar row -->
        <Grid Grid.Row="0" Padding="16,12" BackgroundColor="{AppThemeBinding Light=White, Dark=#1C1C1E}">
            <Label Text="Movies" FontSize="24" FontAttributes="Bold" />
            <Button Text="Filter"
                    HorizontalOptions="End"
                    Command="{Binding OpenFilterCommand}" />
        </Grid>

        <!-- Content -->
        <Grid Grid.Row="1">
            <ActivityIndicator IsRunning="{Binding IsBusy}"
                               IsVisible="{Binding IsBusy}"
                               VerticalOptions="Center"
                               HorizontalOptions="Center" />

            <CollectionView ItemsSource="{Binding Movies}"
                            IsVisible="{Binding IsBusy, Converter={StaticResource InvertedBoolConverter}}">
                <CollectionView.ItemTemplate>
                    <DataTemplate x:DataType="models:Movie">
                        <Grid Padding="16,12" ColumnDefinitions="*,Auto">
                            <VerticalStackLayout Grid.Column="0" Spacing="4">
                                <Label Text="{Binding Title}" FontSize="17" FontAttributes="Bold" />
                                <Label Text="{Binding Genre}" FontSize="14" TextColor="Gray" />
                            </VerticalStackLayout>
                            <Label Grid.Column="1" Text="{Binding Year}" TextColor="Gray"
                                   VerticalOptions="Center" />
                            <Grid.GestureRecognizers>
                                <TapGestureRecognizer
                                    Command="{Binding Source={RelativeSource AncestorType={x:Type viewmodels:BrowseViewModel}}, Path=SelectMovieCommand}"
                                    CommandParameter="{Binding .}" />
                            </Grid.GestureRecognizers>
                        </Grid>
                    </DataTemplate>
                </CollectionView.ItemTemplate>
            </CollectionView>
        </Grid>

    </Grid>

</base:BasePage>
```

- [ ] **Step 2: Create BrowsePage code-behind**

`MauiNavigation/Pages/Browse/BrowsePage.xaml.cs`:
```csharp
using MauiNavigation.Base;
using MauiNavigation.Core.ViewModels;

namespace MauiNavigation.Pages.Browse;

public partial class BrowsePage : BasePage<BrowseViewModel>
{
    public BrowsePage(BrowseViewModel viewModel) : base(viewModel)
    {
        InitializeComponent();
    }
}
```

- [ ] **Step 3: Add InvertedBoolConverter to App.xaml**

`BrowsePage` uses `InvertedBoolConverter` to hide the list while busy. Add it to `App.xaml`'s resource dictionary:

`MauiNavigation/App.xaml` — inside `<Application.Resources><ResourceDictionary>`:
```xml
<toolkit:InvertedBoolConverter x:Key="InvertedBoolConverter" />
```

And add the CommunityToolkit.Maui namespace to `App.xaml`:
```xml
xmlns:toolkit="http://schemas.microsoft.com/dotnet/2022/maui/toolkit"
```

> **Alternative if you want to avoid the extra dependency:** Write a simple `InvertedBoolConverter` class in the MAUI project (`Converters/InvertedBoolConverter.cs`) implementing `IValueConverter`. It's 10 lines. Both paths work — the plan uses the CommunityToolkit version since the package was already added in Task 9.

- [ ] **Step 4: Create FavoritesPage**

`MauiNavigation/Pages/Favorites/FavoritesPage.xaml`:
```xml
<?xml version="1.0" encoding="utf-8" ?>
<base:BasePage
    x:TypeArguments="viewmodels:FavoritesViewModel"
    x:Class="MauiNavigation.Pages.Favorites.FavoritesPage"
    xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
    xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
    xmlns:base="clr-namespace:MauiNavigation.Base"
    xmlns:viewmodels="clr-namespace:MauiNavigation.Core.ViewModels;assembly=MauiNavigation.Core">

    <Grid Padding="16">
        <Label Text="Your favorites will appear here."
               VerticalOptions="Center"
               HorizontalOptions="Center"
               TextColor="Gray" />
    </Grid>

</base:BasePage>
```

`MauiNavigation/Pages/Favorites/FavoritesPage.xaml.cs`:
```csharp
using MauiNavigation.Base;
using MauiNavigation.Core.ViewModels;

namespace MauiNavigation.Pages.Favorites;

public partial class FavoritesPage : BasePage<FavoritesViewModel>
{
    public FavoritesPage(FavoritesViewModel viewModel) : base(viewModel)
    {
        InitializeComponent();
    }
}
```

- [ ] **Step 5: Build to verify**

```bash
dotnet build MauiNavigation/MauiNavigation.csproj -f net10.0-android
```

Expected: Build errors only about `MovieDetailPage`, `FilterPage` — not about Browse or Favorites.

---

### Task 11: MovieDetail and Filter pages, then cleanup

**Files:**
- Create: `MauiNavigation/Pages/MovieDetail/MovieDetailPage.xaml` + `.cs`
- Create: `MauiNavigation/Pages/Filter/FilterPage.xaml` + `.cs`
- Delete: `MauiNavigation/MainPage.xaml` + `MauiNavigation/MainPage.xaml.cs`

- [ ] **Step 1: Create MovieDetailPage**

`MauiNavigation/Pages/MovieDetail/MovieDetailPage.xaml`:
```xml
<?xml version="1.0" encoding="utf-8" ?>
<base:BasePage
    x:TypeArguments="viewmodels:MovieDetailViewModel"
    x:Class="MauiNavigation.Pages.MovieDetail.MovieDetailPage"
    xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
    xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
    xmlns:base="clr-namespace:MauiNavigation.Base"
    xmlns:viewmodels="clr-namespace:MauiNavigation.Core.ViewModels;assembly=MauiNavigation.Core">

    <Grid RowDefinitions="Auto,*" Padding="16">

        <!-- Custom nav bar (BasePage hides Shell nav bar by default) -->
        <Grid Grid.Row="0" ColumnDefinitions="Auto,*" Margin="0,0,0,16">
            <Button Grid.Column="0"
                    Text="← Back"
                    Command="{Binding Source={RelativeSource AncestorType={x:Type viewmodels:MovieDetailViewModel}}, Path=GoBackCommand}" />
            <Label Grid.Column="1"
                   Text="{Binding Title}"
                   FontSize="20"
                   FontAttributes="Bold"
                   VerticalOptions="Center"
                   Margin="8,0" />
        </Grid>

        <!-- Content -->
        <VerticalStackLayout Grid.Row="1" Spacing="12">
            <ActivityIndicator IsRunning="{Binding IsBusy}" IsVisible="{Binding IsBusy}" />
            <Label Text="{Binding Title}" FontSize="24" FontAttributes="Bold" />
            <Label Text="{Binding MovieId, StringFormat='Movie ID: {0}'}" TextColor="Gray" />
            <Label Text="Full details would be loaded from a movie service in a real app."
                   TextColor="Gray" FontSize="14" />
        </VerticalStackLayout>

    </Grid>

</base:BasePage>
```

`MauiNavigation/Pages/MovieDetail/MovieDetailPage.xaml.cs`:
```csharp
using MauiNavigation.Base;
using MauiNavigation.Core.ViewModels;

namespace MauiNavigation.Pages.MovieDetail;

public partial class MovieDetailPage : BasePage<MovieDetailViewModel>
{
    public MovieDetailPage(MovieDetailViewModel viewModel) : base(viewModel)
    {
        InitializeComponent();
    }
}
```

Add `GoBackCommand` to `MovieDetailViewModel` (uses `Facade.Navigation.GoBackAsync`):

Add to `MauiNavigation.Core/ViewModels/MovieDetailViewModel.cs`:
```csharp
[RelayCommand]
private Task GoBack() => Facade.Navigation.GoBackAsync();
```

(Add `using CommunityToolkit.Mvvm.Input;` if not present.)

- [ ] **Step 2: Create FilterPage (modal)**

`MauiNavigation/Pages/Filter/FilterPage.xaml`:
```xml
<?xml version="1.0" encoding="utf-8" ?>
<base:BasePage
    x:TypeArguments="viewmodels:FilterViewModel"
    x:Class="MauiNavigation.Pages.Filter.FilterPage"
    xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
    xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
    xmlns:base="clr-namespace:MauiNavigation.Base"
    xmlns:viewmodels="clr-namespace:MauiNavigation.Core.ViewModels;assembly=MauiNavigation.Core">

    <Grid RowDefinitions="Auto,*,Auto" Padding="24">

        <!-- Header -->
        <Label Grid.Row="0" Text="Filter Movies"
               FontSize="22" FontAttributes="Bold"
               Margin="0,0,0,24" />

        <!-- Fields -->
        <VerticalStackLayout Grid.Row="1" Spacing="16">
            <VerticalStackLayout Spacing="4">
                <Label Text="Genre" FontAttributes="Bold" />
                <Entry Placeholder="e.g. Drama, Sci-Fi, Crime"
                       Text="{Binding Genre}" />
            </VerticalStackLayout>
            <VerticalStackLayout Spacing="4">
                <Label Text="Minimum Year" FontAttributes="Bold" />
                <Entry Placeholder="e.g. 2000"
                       Keyboard="Numeric"
                       Text="{Binding MinYear}" />
            </VerticalStackLayout>
        </VerticalStackLayout>

        <!-- Actions -->
        <Grid Grid.Row="2" ColumnDefinitions="*,*" ColumnSpacing="12" Margin="0,24,0,0">
            <Button Grid.Column="0"
                    Text="Close"
                    Command="{Binding DismissCommand}" />
            <Button Grid.Column="1"
                    Text="Apply"
                    Command="{Binding ApplyCommand}" />
        </Grid>

    </Grid>

</base:BasePage>
```

`MauiNavigation/Pages/Filter/FilterPage.xaml.cs`:
```csharp
using MauiNavigation.Base;
using MauiNavigation.Core.ViewModels;

namespace MauiNavigation.Pages.Filter;

public partial class FilterPage : BasePage<FilterViewModel>
{
    public FilterPage(FilterViewModel viewModel) : base(viewModel)
    {
        InitializeComponent();
    }
}
```

- [ ] **Step 3: Delete MainPage**

```bash
rm MauiNavigation/MainPage.xaml MauiNavigation/MainPage.xaml.cs
```

- [ ] **Step 4: Full build**

```bash
dotnet build MauiNavigation/MauiNavigation.csproj -f net10.0-android
```

Expected: `Build succeeded.`

- [ ] **Step 5: Run all tests one final time**

```bash
dotnet test MauiNavigation.Tests/MauiNavigation.Tests.csproj
```

Expected: All tests pass.

---

### Task 12: Write NAVIGATION_GUIDE.md article

**Files:**
- Create: `NAVIGATION_GUIDE.md` (repo root)

The article covers all 11 sections from the spec. Each section includes the actual code from the implementation above — this task is best done after Tasks 1–11 are complete so you can reference the exact final code.

- [ ] **Step 1: Write article skeleton (all 11 section headings + one-line summaries)**

Create `NAVIGATION_GUIDE.md` with the H1 and all 11 H2 headings filled in.

- [ ] **Step 2: Write Section 1 — Why Shell navigation + the Core/App split**

Explain: MAUI Shell provides URI-based navigation with automatic back stack management. The Core/App project split enforces at compile time that ViewModels have no UI framework dependency — making them testable in plain `net10.0` test projects and portable across platforms.

- [ ] **Step 3: Write Section 2 — Project setup (two projects, NuGet deps)**

Include the exact csproj files from Tasks 1 and 3. Explain: Core gets CommunityToolkit.Mvvm and Microsoft.Extensions.Logging.Abstractions. App gets Microsoft.Maui.Controls (implicit) and adds a project reference to Core.

- [ ] **Step 4: Write Section 3 — INavigationService contract + strongly-typed parameters**

Show `INavigationService.cs`, `Routes.cs`, `MovieDetailParameters.cs`. Explain the `__params` key pattern, `ShellNavigationQueryParameters`, and why `Routes` is needed (Core can't use `nameof(PageType)`).

- [ ] **Step 5: Write Section 4 — BaseViewModelFacade**

Show the full `BaseViewModelFacade.cs`. Explain the facade pattern: one constructor arg instead of N shared dependencies. Senior-level callout: the facade is a Singleton — ViewModels are Transient and share a single Facade instance by design.

- [ ] **Step 6: Write Section 5 — BaseViewModel**

Show the full `BaseViewModel.cs`. Walk through each concept separately:
- Lifecycle wrappers vs virtual hooks (call chain diagram in text)
- CTS management and the `ReferenceEquals` guard
- `ApplyNavigationParameters` + `isGoBack`
- `IsBusy` ref-counting with `Interlocked`
- `SafeFireAndForget` / `ExecuteSafeAsync`

- [ ] **Step 7: Write Section 6 — BasePage**

Show `BasePage.cs`. Explain why `IQueryAttributable` lives here and not in Core. Highlight the bridge: `ApplyQueryAttributes` → `ApplyNavigationParameters`.

- [ ] **Step 8: Write Section 7 — AppShell (TabBar + route registration)**

Show `AppShell.xaml` and `AppShell.xaml.cs`. Explain the two registration paths: `Routing.RegisterRoute` for push routes vs `RegisterModal` for modals. Emphasise that `nameof(MovieDetailPage)` and `Routes.MovieDetail` must be the same string — and how to verify this.

- [ ] **Step 9: Write Section 8 — ShellNavigationService**

Show the full `ShellNavigationService.cs`. Walk through `GoBackAsync` injecting `__isBack`, `GoToAsync<TParams>` using `ShellNavigationQueryParameters`, and `PresentModalAsync<TParams>` calling `ApplyNavigationParameters` directly (because `PushModalAsync` bypasses Shell routing).

- [ ] **Step 10: Write Section 9 — DI wiring in MauiProgram**

Show `MauiProgram.cs` and `App.xaml.cs`. Explain: infrastructure = Singleton, ViewModels + Pages = Transient (fresh per navigation), `AppShell` = Singleton (one shell for the app lifetime), and why `App` must receive `IServiceProvider` to resolve `AppShell`.

- [ ] **Step 11: Write Section 10 — Real-world walk-through**

Trace two flows with prose + code snippets:

**Flow A: Browse → MovieDetail → Back**
1. `BrowsePage` appears → `BrowseViewModel.OnAppearingAsync` loads movies
2. User taps a movie → `SelectMovieCommand` → `GoToAsync(Routes.MovieDetail, new MovieDetailParameters(...))`
3. Shell navigates → `MovieDetailPage.ApplyQueryAttributes` → `MovieDetailViewModel.ApplyNavigationParameters` → `InitializeFromQueryAsync` with `isGoBack=false`
4. User taps Back → `GoBackCommand` → `GoBackAsync` injects `__isBack=true` → Shell pops → `BrowsePage.ApplyQueryAttributes` → `BrowseViewModel.InitializeFromQueryAsync` with `isGoBack=true` → skips reload

**Flow B: Browse → Filter modal → Dismiss**
1. User taps Filter → `OpenFilterCommand` → `PresentModalAsync(Routes.Filter)`
2. `ShellNavigationService` resolves `FilterPage` from DI, calls `PushModalAsync`
3. `FilterPage` appears → `FilterViewModel.OnAppearingAsync`
4. User taps Close/Apply → `DismissModalAsync` → `PopModalAsync`
5. `BrowsePage` appears again → `BrowseViewModel.OnAppearingAsync` runs (this is where post-dismiss refresh logic goes — NOT in `InitializeFromQueryAsync`)

- [ ] **Step 12: Write Section 11 — Gotchas & tips**

Cover:
- **Transient vs Singleton**: Pages must be Transient if ViewModels are Transient. A Singleton page holds a stale ViewModel after first navigation.
- **Modal vs push**: Push via `GoToAsync` = Shell routing = params via `ShellNavigationQueryParameters`. Modal via `PresentModalAsync` = `PushModalAsync` = params delivered directly before push.
- **`isGoBack` is only for push back-navigation**: `DismissModalAsync` (`PopModalAsync`) does NOT trigger `ApplyQueryAttributes` on the page below. Post-dismiss refresh must go in `OnAppearingAsync`.
- **`nameof()` and `Routes` must stay in sync**: If you rename a page class, update both `nameof(...)` in `AppShell` AND the `Routes` constant.
- **`IsBusy` and nested async**: The ref-counted `busyCount` means you can call `SafeFireAndForget` multiple times concurrently without the spinner flickering off early.

---

## Plan Review Loop

After writing the complete plan, dispatch a plan-document-reviewer subagent:

**Plan:** `docs/superpowers/plans/2026-03-27-maui-navigation-guide.md`
**Spec:** `docs/superpowers/specs/2026-03-27-maui-navigation-guide-design.md`
