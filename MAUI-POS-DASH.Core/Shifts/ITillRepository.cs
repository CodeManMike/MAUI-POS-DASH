namespace MAUI_POS_DASH.Core.Shifts;

/// <summary>
/// We keep Till persistence behind this interface — one Till per shift, created when the shift
/// opens and updated by whichever payment module records a tender against it.
/// </summary>
public interface ITillRepository
{
    Task CreateAsync(Till till, CancellationToken cancellationToken = default);

    Task<Till?> GetByShiftIdAsync(Guid shiftId, CancellationToken cancellationToken = default);

    Task UpdateAsync(Till till, CancellationToken cancellationToken = default);
}
