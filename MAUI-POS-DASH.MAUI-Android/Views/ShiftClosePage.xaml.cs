using MAUI_POS_DASH.MAUI_Android.ViewModels;

namespace MAUI_POS_DASH.MAUI_Android.Views;

/// <summary>Construction and lifecycle plumbing only — see <see cref="ShiftCloseViewModel"/>.</summary>
public partial class ShiftClosePage : ContentPage
{
    #region Fields
    private readonly ShiftCloseViewModel _viewModel;
    #endregion

    #region Constructor
    public ShiftClosePage(ShiftCloseViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }
    #endregion

    #region Overrides
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.OnAppearingAsync();
    }
    #endregion
}
