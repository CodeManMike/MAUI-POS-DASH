using CommunityToolkit.Mvvm.Input;
using MAUI_POS_DASH.Core.Attendants;

namespace MAUI_POS_DASH.MAUI_Android.ViewModels;

/// <summary>We own the terminal home menu — one command per tile, each a push navigation.</summary>
public partial class HomeViewModel : AuthenticatedViewModelBase
{
    #region Constructor
    public HomeViewModel(AttendantService attendantService) : base(attendantService)
    {
    }
    #endregion

    #region Commands
    [RelayCommand]
    private Task GoToFleetCardAsync() => Shell.Current.GoToAsync(RouteNames.FleetCardSale);

    [RelayCommand]
    private Task GoToMobileMoneyAsync() => Shell.Current.GoToAsync(RouteNames.MobileMoneySale);

    [RelayCommand]
    private Task GoToCashAsync() => Shell.Current.GoToAsync(RouteNames.CashSale);

    [RelayCommand]
    private Task GoToAttendantsAsync() => Shell.Current.GoToAsync(RouteNames.AttendantManagement);

    [RelayCommand]
    private Task GoToOpenShiftAsync() => Shell.Current.GoToAsync(RouteNames.ShiftOpen);

    [RelayCommand]
    private Task GoToCloseShiftAsync() => Shell.Current.GoToAsync(RouteNames.ShiftClose);
    #endregion
}
