namespace MAUI_POS_DASH.Core.Shifts;

/// <summary>
/// We own the shift lifecycle — opening a shift for an attendant and closing it out again.
/// </summary>
public class ShiftService
{
    #region Fields
    private readonly IShiftRepository _shiftRepository;
    private readonly ITillRepository _tillRepository;
    private readonly TillReconciliationService _tillReconciliationService;
    #endregion

    #region Constructor
    public ShiftService(
        IShiftRepository shiftRepository,
        ITillRepository tillRepository,
        TillReconciliationService tillReconciliationService)
    {
        _shiftRepository = shiftRepository;
        _tillRepository = tillRepository;
        _tillReconciliationService = tillReconciliationService;
    }
    #endregion

    #region Public Methods
    /// <summary>
    /// We expose this so pages can find the shift to close without depending on
    /// IShiftRepository directly — pages talk to services, not repositories.
    /// </summary>
    public Task<Shift?> GetActiveShiftAsync(Guid attendantId, CancellationToken cancellationToken = default) =>
        _shiftRepository.GetActiveShiftAsync(attendantId, cancellationToken);

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
        var openedShift = await _shiftRepository.OpenAsync(shift, cancellationToken);

        var till = new Till
        {
            Id = Guid.NewGuid(),
            ShiftId = openedShift.Id,
            CashTotal = 0,
            FleetCardTotal = 0,
            MobileMoneyTotal = 0
        };
        await _tillRepository.CreateAsync(till, cancellationToken);

        return openedShift;
    }

    /// <summary>
    /// We compare what the till should have against what the attendant counted, without writing
    /// anything — ShiftClose.razor shows this before the attendant confirms the close is final.
    /// </summary>
    public async Task<ReconciliationResult> PreviewCloseAsync(Shift shift, decimal cashCounted, CancellationToken cancellationToken = default)
    {
        var till = await _tillRepository.GetByShiftIdAsync(shift.Id, cancellationToken)
            ?? throw new InvalidOperationException($"Shift {shift.Id} has no till — every open shift should have one.");

        return _tillReconciliationService.Reconcile(till, cashCounted);
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
