using MAUI_POS_DASH.Core.Attendants;

namespace MAUI_POS_DASH.Core.Persistence.Repositories;

/// <summary>
/// We implement IAttendantSessionStore against TerminalDbContext.
/// </summary>
public class EfAttendantSessionStore : IAttendantSessionStore
{
    #region Fields
    private readonly TerminalDbContext _dbContext;
    #endregion

    #region Constructor
    public EfAttendantSessionStore(TerminalDbContext dbContext)
    {
        _dbContext = dbContext;
    }
    #endregion

    #region Public Methods
    /// <summary>
    /// We include the Attendant navigation — every caller of this method wants the attendant's
    /// name/role alongside the session, not just the bare AttendantId.
    /// </summary>
    public Task<AttendantSession?> GetActiveSessionAsync(CancellationToken cancellationToken = default)
    {
        return _dbContext.AttendantSessions
            .Include(session => session.Attendant)
            .Where(session => session.SignedOutAt == null)
            .SingleOrDefaultAsync(cancellationToken);
    }

    /// <summary>
    /// We sign out any existing active session before starting a new one — a terminal has one
    /// attendant signed in at a time, so this covers the case where a previous attendant forgot
    /// to sign out (or the app restarted) before the next one taps in.
    /// </summary>
    public async Task SignInAsync(Guid attendantId, CancellationToken cancellationToken = default)
    {
        await SignOutAsync(cancellationToken);

        _dbContext.AttendantSessions.Add(new AttendantSession
        {
            Id = Guid.NewGuid(),
            AttendantId = attendantId,
            SignedInAt = DateTimeOffset.UtcNow
        });

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task SignOutAsync(CancellationToken cancellationToken = default)
    {
        var active = await _dbContext.AttendantSessions
            .Where(session => session.SignedOutAt == null)
            .SingleOrDefaultAsync(cancellationToken);

        if (active is null)
        {
            return;
        }

        active.SignedOutAt = DateTimeOffset.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
    #endregion
}
