namespace MAUI_POS_DASH.Core.Shifts;

public record ReconciliationResult(decimal ExpectedCash, decimal CountedCash, decimal Variance, bool WithinTolerance);

/// <summary>
/// We compare a till's recorded totals against what an attendant physically counted at close-out.
/// </summary>
public class TillReconciliationService
{
    #region Fields
    private const decimal ToleranceAmount = 0.50m;
    #endregion

    #region Public Methods
    /// <summary>
    /// We compare the till's recorded cash total against what the attendant physically counted,
    /// flagging anything outside a small tolerance for a supervisor to review. A real till never
    /// balances to the cent — a small tolerance is normal, not a bug.
    /// </summary>
    public ReconciliationResult Reconcile(Till till, decimal countedCash)
    {
        var variance = countedCash - till.CashTotal;
        var withinTolerance = Math.Abs(variance) <= ToleranceAmount;

        return new ReconciliationResult(till.CashTotal, countedCash, variance, withinTolerance);
    }
    #endregion
}
