namespace MAUI_POS_DASH.Core.Contracts;

/// <summary>
/// We mirror Till's per-tender totals and add GrandTotal as a derived convenience for the UI —
/// GrandTotal isn't mapped from the entity, it's computed here from the three totals.
/// </summary>
public record TillDto(Guid Id, Guid ShiftId, decimal CashTotal, decimal FleetCardTotal, decimal MobileMoneyTotal)
{
    public decimal GrandTotal => CashTotal + FleetCardTotal + MobileMoneyTotal;
}
