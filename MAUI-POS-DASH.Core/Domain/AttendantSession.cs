namespace MAUI_POS_DASH.Core.Domain;

/// <summary>
/// We represent one attendant's period of being signed in to this specific terminal. A row with
/// SignedOutAt still null is the currently active session. This is terminal-local state — it
/// lives only in TerminalDbContext, never synced to the back office.
/// </summary>
public class AttendantSession
{
    #region Properties
    public Guid Id { get; set; }

    public Guid AttendantId { get; set; }

    public Attendant? Attendant { get; set; }

    public DateTimeOffset SignedInAt { get; set; }

    public DateTimeOffset? SignedOutAt { get; set; }
    #endregion
}
