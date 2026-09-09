using MAUI_POS_DASH.Core.Shifts;

namespace MAUI_POS_DASH.Core.Sales;

/// <summary>
/// We centralize the two checks every payment service must pass before recording a sale — this
/// used to be a Till-only lookup repeated almost verbatim in FleetCardSaleService, CashSaleService,
/// and MobileMoneySaleService; putting the shift-status and currency checks here too means fixing
/// this class of bug once instead of three times.
/// </summary>
public static class SalePreconditions
{
    #region Fields
    // numeric(18,2) on the backoffice side allows 16 integer digits and 2 fractional digits — the
    // largest value that column can hold without PostgreSQL rounding or overflowing it on sync, so
    // we reject anything that wouldn't round-trip through it before it's ever accepted here.
    private const decimal MaxStorableAmount = 9_999_999_999_999_999.99m;
    #endregion

    #region Public Methods
    /// <summary>
    /// We reject an amount here, at the point of sale, rather than accepting it locally and only
    /// discovering it can't round-trip once it syncs to the backoffice's numeric(18,2) column — by
    /// then the attendant has already seen the sale complete.
    /// </summary>
    public static void ValidateCurrencyAmount(decimal amount, string paramName, string context)
    {
        if (amount > MaxStorableAmount || decimal.Round(amount, 2) != amount)
        {
            throw new ArgumentException(
                $"{context} must be at most two decimal places and no greater than {MaxStorableAmount:N2}.",
                paramName);
        }
    }

    /// <summary>
    /// We look up the shift and its till together, confirming the shift is still Open before any
    /// payment service is allowed to record a sale against it — a closed/reconciled shift should
    /// never silently gain a new sale after the attendant has already counted out and gone home.
    /// We look up the Till only after confirming the shift is Open, and before anything else is
    /// written — checking later would leave an orphaned Sale/Transaction if a shift somehow lacks
    /// a Till, since the write that follows has no ambient transaction spanning both.
    /// </summary>
    public static async Task<Till> GetOpenShiftTillAsync(
        IShiftRepository shiftRepository,
        ITillRepository tillRepository,
        Guid shiftId,
        CancellationToken cancellationToken = default)
    {
        Shift shift = await shiftRepository.GetByIdAsync(shiftId, cancellationToken)
            ?? throw new InvalidOperationException($"Shift {shiftId} doesn't exist.");

        if (shift.Status != ShiftStatus.Open)
        {
            throw new InvalidOperationException($"Shift {shiftId} is closed — a closed shift can't record new sales.");
        }

        return await tillRepository.GetByShiftIdAsync(shiftId, cancellationToken)
            ?? throw new InvalidOperationException($"Shift {shiftId} has no till — every open shift should have one.");
    }
    #endregion
}
