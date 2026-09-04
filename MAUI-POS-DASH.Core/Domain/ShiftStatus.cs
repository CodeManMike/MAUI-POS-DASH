namespace MAUI_POS_DASH.Core.Domain;

/// <summary>
/// We track whether a shift is still being worked (<see cref="Open"/>) or has been counted out
/// and finished (<see cref="Closed"/>).
/// </summary>
public enum ShiftStatus
{
    Open,
    Closed
}
