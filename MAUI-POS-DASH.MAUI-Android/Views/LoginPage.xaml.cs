using MAUI_POS_DASH.MAUI_Android.ViewModels;

namespace MAUI_POS_DASH.MAUI_Android.Views;

/// <summary>
/// We keep this to construction and lifecycle plumbing only — everything the login flow actually
/// does lives in <see cref="LoginViewModel"/>.
/// </summary>
public partial class LoginPage : ContentPage
{
    #region Fields
    private readonly LoginViewModel _viewModel;
    #endregion

    #region Constructor
    public LoginPage(LoginViewModel viewModel)
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
