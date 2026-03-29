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
