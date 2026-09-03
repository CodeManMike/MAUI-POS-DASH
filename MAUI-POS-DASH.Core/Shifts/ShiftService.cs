namespace MAUI_POS_DASH.Core.Shifts;

/// <summary>
/// We own the shift lifecycle — opening a shift for an attendant and closing it out again.
/// </summary>
public class ShiftService
{
    #region Fields
    private readonly IShiftRepository _shiftRepository;
    #endregion

    #region Constructor
    public ShiftService(IShiftRepository shiftRepository)
    {
        _shiftRepository = shiftRepository;
    }
    #endregion

    #region Public Methods
    /// <summary>
    /// We open a new shift for the given attendant with the counted opening float. We refuse if
    /// that attendant already has an open shift — one attendant, one till, one shift at a time.
    /// </summary>
    public async Task<Shift> OpenShiftAsync(Guid attendantId, decimal openingFloat, CancellationToken cancellationToken = default)
    {
        var existing = await _shiftRepository.GetActiveShiftAsync(attendantId, cancellationToken);
        if (existing is not null)
        {
            throw new InvalidOperationException(
                $"We can't open a new shift for attendant {attendantId} — shift {existing.Id} is still open.");
        }

        var shift = new Shift
        {
            Id = Guid.NewGuid(),
            AttendantId = attendantId,
            OpenedAt = DateTimeOffset.UtcNow,
            OpeningFloat = openingFloat,
            Status = ShiftStatus.Open
        };

        return await _shiftRepository.OpenAsync(shift, cancellationToken);
    }

    /// <summary>
    /// We close the shift and record what the attendant counted in the till.
    /// TODO(Builder/Overmind): once the Cash module lands, run TillReconciliationService here
    /// and surface the variance before allowing close to complete.
    /// </summary>
    public Task CloseShiftAsync(Shift shift, decimal closingCashCounted, CancellationToken cancellationToken = default)
    {
        shift.ClosedAt = DateTimeOffset.UtcNow;
        shift.ClosingCashCounted = closingCashCounted;
        shift.Status = ShiftStatus.Closed;

        return _shiftRepository.CloseAsync(shift, cancellationToken);
    }
    #endregion
}
