namespace MAUI_POS_DASH.MAUI_Android;

/// <summary>
/// We centralize Shell route names here so a typo in a route string is a compile-time reference
/// error, not a silent runtime navigation failure.
/// </summary>
public static class RouteNames
{
    /// <summary>Route to the sign-in screen.</summary>
    public const string Login = "LoginPage";

    /// <summary>Route to the terminal home/dashboard screen.</summary>
    public const string Home = "HomePage";

    /// <summary>Route to the Manager-only attendant CRUD screen.</summary>
    public const string AttendantManagement = "AttendantManagementPage";

    /// <summary>Route to the open-shift screen.</summary>
    public const string ShiftOpen = "ShiftOpenPage";

    /// <summary>Route to the close-shift/reconciliation screen.</summary>
    public const string ShiftClose = "ShiftClosePage";

    /// <summary>Route to the fleet card payment screen.</summary>
    public const string FleetCardSale = "FleetCardSalePage";

    /// <summary>Route to the cash payment screen.</summary>
    public const string CashSale = "CashSalePage";

    /// <summary>Route to the mobile money payment screen.</summary>
    public const string MobileMoneySale = "MobileMoneySalePage";
}
