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
