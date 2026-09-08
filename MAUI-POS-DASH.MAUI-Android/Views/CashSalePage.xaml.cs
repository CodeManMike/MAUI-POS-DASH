using MAUI_POS_DASH.MAUI_Android.ViewModels;

namespace MAUI_POS_DASH.MAUI_Android.Views;

/// <summary>Construction and lifecycle plumbing only — see <see cref="CashSaleViewModel"/>.</summary>
public partial class CashSalePage : ContentPage
{
    #region Fields
    private readonly CashSaleViewModel _viewModel;
    #endregion

    #region Constructor
    public CashSalePage(CashSaleViewModel viewModel)
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
