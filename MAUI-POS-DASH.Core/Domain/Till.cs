namespace MAUI_POS_DASH.Core.Domain;

/// <summary>
/// We keep one running total per tender type for a shift, so reconciliation at close-out has
/// something concrete to compare the attendant's physical count against.
/// </summary>
public class Till
{
    #region Properties
    public Guid Id { get; set; }

    public Guid ShiftId { get; set; }

    public decimal CashTotal { get; set; }

    public decimal FleetCardTotal { get; set; }

    public decimal MobileMoneyTotal { get; set; }
    #endregion
}
