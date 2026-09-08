namespace MAUI_POS_DASH.MAUI_Android.ViewModels;

/// <summary>
/// We let a page's code-behind notify its ViewModel that the page appeared, without the
/// code-behind knowing what — if anything — that ViewModel does about it. This is the one hook
/// code-behind is allowed to call directly; everything past that call is the ViewModel's problem.
/// </summary>
public interface IAppearingAware
{
    Task OnAppearingAsync();
}
