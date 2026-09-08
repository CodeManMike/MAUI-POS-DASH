namespace MAUI_POS_DASH.MAUI_Android;

/// <summary>
/// We centralize Shell route names here so a typo in a route string is a compile-time reference
/// error, not a silent runtime navigation failure.
/// </summary>
public static class RouteNames
{
    public const string Login = "LoginPage";
    public const string Home = "HomePage";
    public const string AttendantManagement = "AttendantManagementPage";
    public const string ShiftOpen = "ShiftOpenPage";
    public const string ShiftClose = "ShiftClosePage";
    public const string FleetCardSale = "FleetCardSalePage";
    public const string CashSale = "CashSalePage";
    public const string MobileMoneySale = "MobileMoneySalePage";
}
