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

    [ObservableProperty]
    private string _description = string.Empty;

    public override Task InitializeFromQueryAsync(IDictionary<string, object> query, bool isGoBack, CancellationToken ct)
    {
        // Skip re-loading when returning back to this page from a child.
        if (isGoBack) return Task.CompletedTask;

        if (query.TryGetValue("__params", out var p) && p is MovieDetailParameters parms)
        {
            MovieId = parms.MovieId;
            Title = parms.Title;
            // Load full movie details with error handling
            SafeFireAndForgetWithErrorAlert(LoadMovieDetailsAsync, showLoader: true, errorTitle: "Failed to load movie");
        }

        return Task.CompletedTask;
    }

    private async Task LoadMovieDetailsAsync(CancellationToken ct)
    {
        // Simulate API call
        await Task.Delay(500, ct);

        // In a real app: var movie = await movieService.GetByIdAsync(MovieId, ct);
        Description = $"This is the description for movie #{MovieId}. In a real app, this would be fetched from an API.";

        // Example: Uncomment to test error handling
        // throw new InvalidOperationException("Simulated API failure");
    }

    [RelayCommand]
    private Task GoBack() => Facade.Navigation.GoBackAsync();

    /// <summary>
    /// Example command showing manual error handling with custom message.
    /// </summary>
    [RelayCommand]
    private void RefreshDetails()
    {
        SafeFireAndForget(
            LoadMovieDetailsAsync,
            showLoader: true,
            onError: ex => Facade.Alerts.ShowErrorAsync(
                $"Could not refresh: {ex.Message}",
                "Refresh Failed"));
    }
}
