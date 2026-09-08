using MAUI_POS_DASH.MAUI_Android.ViewModels;

namespace MAUI_POS_DASH.MAUI_Android.Views;

/// <summary>Construction and lifecycle plumbing only — see <see cref="FleetCardSaleViewModel"/>.</summary>
public partial class FleetCardSalePage : ContentPage
{
    #region Fields
    private readonly FleetCardSaleViewModel _viewModel;
    #endregion

    #region Constructor
    public FleetCardSalePage(FleetCardSaleViewModel viewModel)
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
