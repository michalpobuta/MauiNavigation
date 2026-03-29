# MAUI Navigation Framework

A production-ready navigation architecture for .NET MAUI applications. Built with testability, scalability, and clean architecture in mind.

## Features

- **Core/App Separation** — ViewModels in a platform-agnostic Core project, testable without MAUI references
- **Strongly-Typed Navigation** — No magic strings, compile-time checked parameters
- **Lifecycle Management** — `OnAppearingAsync`, `OnDisappearingAsync` with automatic cancellation
- **Ref-Counted Loading** — `IsBusy` handles concurrent async operations correctly
- **Modal Results** — Present modals and await their result with `NavigationResult<T>`
- **Navigation Guards** — Prevent data loss with unsaved changes confirmation
- **Deep Linking** — Handle `mauinavigation://` and App Links out of the box
- **Tab Navigation** — Switch tabs and navigate within them programmatically
- **Shared State** — Event-driven services for cross-page state (filters, user preferences)
- **Fully Testable** — 34 unit tests included, mock-friendly interfaces

## Quick Start

```csharp
// Navigate with parameters
await Navigation.GoToAsync(Routes.MovieDetail, new MovieDetailParams(42, "Inception"));

// Present modal and get result
var result = await Navigation.PresentModalForResultAsync<FilterResult>(Routes.Filter);
if (result.TryGetValue(out var filter))
{
    // User applied filter
}

// Switch tabs
await Navigation.SwitchTabAsync(Routes.FavoritesTab);
```

## Project Structure

```
MauiNavigation/
├── MauiNavigation.Core/          # Platform-agnostic (ViewModels, Services, Interfaces)
│   ├── Base/                     # BaseViewModel, BaseViewModelFacade
│   ├── Navigation/               # INavigationService, Routes, Parameters
│   ├── Services/                 # IAlertService, IFilterService
│   └── ViewModels/               # App ViewModels
├── MauiNavigation/               # MAUI App (Pages, Platform code)
│   ├── Infrastructure/           # ShellNavigationService, AlertService
│   ├── Pages/                    # XAML pages with code-behind
│   └── Platforms/                # Android/iOS configuration
└── MauiNavigation.Tests/         # Unit tests
```

## Documentation

📖 **[NAVIGATION_GUIDE.md](NAVIGATION_GUIDE.md)** — Comprehensive guide (2200+ lines, 20 sections)

### Fundamentals (Sections 1-11)
- Shell navigation basics
- INavigationService contract
- BaseViewModel lifecycle
- Parameter passing
- DI setup

### Advanced Patterns (Sections 12-20)
- Error handling with IAlertService
- Modal result pattern
- Tab navigation
- Navigation guards
- Deep linking
- Shared state services
- Unit testing ViewModels

## Requirements

- .NET 10.0+
- Visual Studio 2022 / Rider / VS Code
- Android SDK / Xcode (for mobile targets)

## Running the App

```bash
# Build
dotnet build

# Run tests
dotnet test MauiNavigation.Tests

# Run on Android
dotnet build -t:Run -f net10.0-android

# Run on iOS
dotnet build -t:Run -f net10.0-ios
```

## Key Interfaces

| Interface | Purpose |
|-----------|---------|
| `INavigationService` | All navigation operations |
| `IAlertService` | Alerts, toasts, confirmations |
| `IFilterService` | Shared filter state with events |
| `IModalResultProvider<T>` | Modals that return results |
| `INavigationGuard` | Unsaved changes protection |
| `IDeepLinkHandler` | Handle incoming deep links |

## License

MIT
