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
