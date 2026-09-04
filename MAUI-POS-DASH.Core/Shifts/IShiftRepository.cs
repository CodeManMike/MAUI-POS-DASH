namespace MAUI_POS_DASH.Core.Shifts;

/// <summary>
/// We keep ShiftService's storage access behind this interface so it doesn't know or care
/// whether shifts live in SQLite, Postgres, or an in-memory fixture in a test.
/// </summary>
public interface IShiftRepository
{
    Task<Shift?> GetActiveShiftAsync(Guid attendantId, CancellationToken cancellationToken = default);

    Task<Shift> OpenAsync(Shift shift, CancellationToken cancellationToken = default);

    Task CloseAsync(Shift shift, CancellationToken cancellationToken = default);
}
