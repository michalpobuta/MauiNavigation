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
