using MAUI_POS_DASH.MAUI_Android.ViewModels;

namespace MAUI_POS_DASH.MAUI_Android.Views;

/// <summary>Construction and lifecycle plumbing only — see <see cref="MobileMoneySaleViewModel"/>.</summary>
public partial class MobileMoneySalePage : ContentPage
{
    #region Fields
    private readonly MobileMoneySaleViewModel _viewModel;
    #endregion

    #region Constructor
    public MobileMoneySalePage(MobileMoneySaleViewModel viewModel)
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
