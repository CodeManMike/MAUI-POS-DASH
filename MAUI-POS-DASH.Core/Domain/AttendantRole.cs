namespace MAUI_POS_DASH.Core.Domain;

/// <summary>
/// We use this to gate what an attendant can do on the terminal — a plain Attendant can ring
/// sales, a Supervisor can approve variances, a Manager can do both plus manage other attendants.
/// </summary>
public enum AttendantRole
{
    Attendant,
    Supervisor,
    Manager
}
