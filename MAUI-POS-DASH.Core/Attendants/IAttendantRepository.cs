namespace MAUI_POS_DASH.Core.Attendants;

/// <summary>
/// We keep AttendantService's storage access behind this interface so it doesn't know or care
/// whether attendants live in SQLite, Postgres, or an in-memory fixture in a test.
/// </summary>
public interface IAttendantRepository
{
    Task<IReadOnlyList<Attendant>> GetActiveAttendantsAsync(CancellationToken cancellationToken = default);

    Task<Attendant?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task AddAsync(Attendant attendant, CancellationToken cancellationToken = default);

    Task UpdateAsync(Attendant attendant, CancellationToken cancellationToken = default);
}
