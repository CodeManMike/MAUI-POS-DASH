namespace MAUI_POS_DASH.Core.Attendants;

/// <summary>
/// We track which attendant is currently signed in to this terminal. Session state is
/// terminal-local — see AttendantSession's own doc comment.
/// </summary>
public interface IAttendantSessionStore
{
    Task<AttendantSession?> GetActiveSessionAsync(CancellationToken cancellationToken = default);

    Task SignInAsync(Guid attendantId, CancellationToken cancellationToken = default);

    Task SignOutAsync(CancellationToken cancellationToken = default);
}
