namespace MAUI_POS_DASH.Core.Domain;

/// <summary>
/// We represent one attendant's time on the till, from open to close, including the float they
/// opened with and (once closed) what they counted at the end.
/// </summary>
public class Shift
{
    #region Properties
    public Guid Id { get; set; }

    public Guid AttendantId { get; set; }

    public Attendant? Attendant { get; set; }

    public DateTimeOffset OpenedAt { get; set; }

    public DateTimeOffset? ClosedAt { get; set; }

    public decimal OpeningFloat { get; set; }

    public decimal? ClosingCashCounted { get; set; }

    public ShiftStatus Status { get; set; }

    public Till? Till { get; set; }
    #endregion
}
