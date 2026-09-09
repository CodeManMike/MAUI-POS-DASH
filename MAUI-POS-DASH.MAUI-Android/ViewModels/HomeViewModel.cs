using CommunityToolkit.Mvvm.Input;
using MAUI_POS_DASH.Core.Attendants;
using MAUI_POS_DASH.Core.Sync;

namespace MAUI_POS_DASH.MAUI_Android.ViewModels;

/// <summary>
/// We own the terminal home menu — one command per tile. Every page in this app is declared as
/// its own top-level ShellContent (see AppShell.xaml), so navigation between them always uses the
/// absolute "//route" form — that's the form Shell guarantees switches the active item correctly,
/// where a bare relative route is only guaranteed to push within the current one.
/// </summary>
public partial class HomeViewModel : AuthenticatedViewModelBase
{
    #region Fields
    private readonly OfflineTransactionQueue _offlineTransactionQueue;
    #endregion

    #region Constructor
    public HomeViewModel(AttendantService attendantService, OfflineTransactionQueue offlineTransactionQueue) : base(attendantService)
    {
        _offlineTransactionQueue = offlineTransactionQueue;
    }
    #endregion

    #region Protected Methods
    /// <summary>
    /// We opportunistically flush any pending transactions every time the attendant lands back on
    /// Home — this is the one screen every attendant passes through constantly, so it's a natural
    /// place to retry a sync that failed while the terminal was offline, without needing a
    /// dedicated background timer.
    /// </summary>
    protected override Task OnAuthenticatedAppearingAsync() => _offlineTransactionQueue.TryFlushAsync();
    #endregion

    #region Commands
    [RelayCommand]
    private Task GoToFleetCardAsync() => Shell.Current.GoToAsync($"//{RouteNames.FleetCardSale}");

    [RelayCommand]
    private Task GoToMobileMoneyAsync() => Shell.Current.GoToAsync($"//{RouteNames.MobileMoneySale}");

    [RelayCommand]
    private Task GoToCashAsync() => Shell.Current.GoToAsync($"//{RouteNames.CashSale}");

    [RelayCommand]
    private Task GoToAttendantsAsync() => Shell.Current.GoToAsync($"//{RouteNames.AttendantManagement}");

    [RelayCommand]
    private Task GoToOpenShiftAsync() => Shell.Current.GoToAsync($"//{RouteNames.ShiftOpen}");

    [RelayCommand]
    private Task GoToCloseShiftAsync() => Shell.Current.GoToAsync($"//{RouteNames.ShiftClose}");
    #endregion
}
