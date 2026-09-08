using MAUI_POS_DASH.MAUI_Android.ViewModels;

namespace MAUI_POS_DASH.MAUI_Android.Views;

/// <summary>Construction and lifecycle plumbing only — see <see cref="ShiftOpenViewModel"/>.</summary>
public partial class ShiftOpenPage : ContentPage
{
    #region Fields
    private readonly ShiftOpenViewModel _viewModel;
    #endregion

    #region Constructor
    public ShiftOpenPage(ShiftOpenViewModel viewModel)
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
