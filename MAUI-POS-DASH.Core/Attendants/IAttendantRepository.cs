namespace MAUI_POS_DASH.Core.Attendants;

/// <summary>
/// We keep AttendantService's storage access behind this interface so it doesn't know or care
/// whether attendants live in SQLite, Postgres, or an in-memory fixture in a test.
/// </summary>
public interface IAttendantRepository
{
    Task<IReadOnlyList<Attendant>> GetActiveAttendantsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// We check every attendant here, active or not — this exists specifically so seeding logic
    /// can tell "nobody has ever been created" apart from "everybody's been deactivated." The two
    /// look the same through GetActiveAttendantsAsync alone, and conflating them would let
    /// deactivating every attendant silently recreate the default PIN-0000 Manager.
    /// </summary>
    Task<bool> AnyExistAsync(CancellationToken cancellationToken = default);

    Task<Attendant?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task AddAsync(Attendant attendant, CancellationToken cancellationToken = default);

    Task UpdateAsync(Attendant attendant, CancellationToken cancellationToken = default);
}
