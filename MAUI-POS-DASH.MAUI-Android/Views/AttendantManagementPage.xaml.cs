using MAUI_POS_DASH.MAUI_Android.ViewModels;

namespace MAUI_POS_DASH.MAUI_Android.Views;

/// <summary>Construction and lifecycle plumbing only — see <see cref="AttendantManagementViewModel"/>.</summary>
public partial class AttendantManagementPage : ContentPage
{
    #region Fields
    private readonly AttendantManagementViewModel _viewModel;
    #endregion

    #region Constructor
    public AttendantManagementPage(AttendantManagementViewModel viewModel)
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
