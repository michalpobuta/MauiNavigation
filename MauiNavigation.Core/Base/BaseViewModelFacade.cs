using MauiNavigation.Core.Navigation;
using MauiNavigation.Core.Services;
using Microsoft.Extensions.Logging;

namespace MauiNavigation.Core.Base;

/// <summary>
/// Aggregates shared services so ViewModels have a single constructor dependency.
/// Registered as Singleton in DI.
/// </summary>
public class BaseViewModelFacade(
    INavigationService navigation,
    IAlertService alerts,
    ILogger<BaseViewModelFacade> logger)
{
    public INavigationService Navigation { get; } = navigation;
    public IAlertService Alerts { get; } = alerts;
    public ILogger Logger { get; } = logger;
}
