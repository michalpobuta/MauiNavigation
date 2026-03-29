using MauiNavigation.Core.Base;
using MauiNavigation.Core.Navigation;
using MauiNavigation.Tests.Mocks;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace MauiNavigation.Tests.Base;

// Concrete testable ViewModel — internal so it doesn't leak outside the test assembly.
// Cannot use `file` modifier: CommunityToolkit source generators crash on file-scoped
// types that inherit ObservableValidator (their compiler-mangled names contain '<'/'>'
// which are illegal in generator hint names).
internal sealed class TestViewModel(BaseViewModelFacade facade) : BaseViewModel<TestViewModel>(facade)
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

    public void SafeFireAndForgetPublic(Func<CancellationToken, Task> action, bool showLoader = false)
        => SafeFireAndForget(action, showLoader);
}

internal static class Facades
{
    public static BaseViewModelFacade Create() => new(
        Substitute.For<INavigationService>(),
        NullAlertService.Instance,
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

    [Fact]
    public async Task IsBusy_IsTrueWhileRunning_AndFalseAfter()
    {
        var vm = new TestViewModel(Facades.Create());
        await vm.OnAppearingInternal();

        var tcs = new TaskCompletionSource();
        bool busyDuringAction = false;

        vm.SafeFireAndForgetPublic(async ct =>
        {
            busyDuringAction = vm.IsBusy;
            await tcs.Task;
        }, showLoader: true);

        // Give fire-and-forget a moment to start
        await Task.Delay(50);
        Assert.True(vm.IsBusy, "IsBusy should be true while action is running");

        tcs.SetResult();
        await Task.Delay(50);
        Assert.False(vm.IsBusy, "IsBusy should be false after action completes");
    }

    [Fact]
    public async Task IsBusy_RefCounting_StaysTrueUntilAllOperationsComplete()
    {
        var vm = new TestViewModel(Facades.Create());
        await vm.OnAppearingInternal();

        var tcs1 = new TaskCompletionSource();
        var tcs2 = new TaskCompletionSource();

        vm.SafeFireAndForgetPublic(async ct => await tcs1.Task, showLoader: true);
        vm.SafeFireAndForgetPublic(async ct => await tcs2.Task, showLoader: true);

        await Task.Delay(50);
        Assert.True(vm.IsBusy, "IsBusy should be true while both are running");

        tcs1.SetResult(); // first completes
        await Task.Delay(50);
        Assert.True(vm.IsBusy, "IsBusy should still be true — second is still running");

        tcs2.SetResult(); // second completes
        await Task.Delay(50);
        Assert.False(vm.IsBusy, "IsBusy should be false now that both completed");
    }

    [Fact]
    public async Task IsBusy_ReturnsFalse_EvenWhenActionThrows()
    {
        var vm = new TestViewModel(Facades.Create());
        await vm.OnAppearingInternal();

        vm.SafeFireAndForgetPublic(ct => throw new InvalidOperationException("test error"), showLoader: true);

        await Task.Delay(50);
        Assert.False(vm.IsBusy, "IsBusy should be false after action throws");
    }
}
