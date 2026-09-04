namespace MAUI_POS_DASH.Core.Domain;

/// <summary>
/// We represent one rung-up sale — one or more fuel/product lines paid for by one or more
/// transactions (split tender is a real forecourt scenario, so Sale and Transaction are
/// separate entities rather than one flat record).
/// </summary>
public class Sale
{
    #region Properties
    public Guid Id { get; set; }

    public Guid ShiftId { get; set; }

    public DateTimeOffset OccurredAt { get; set; }

    public List<SaleLine> Lines { get; set; } = [];

    public decimal Total => Lines.Sum(line => line.LineTotal);
    #endregion
}
