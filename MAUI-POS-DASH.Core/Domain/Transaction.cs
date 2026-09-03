namespace MAUI_POS_DASH.Core.Domain;

/// <summary>
/// We represent one payment against a Sale. This is the unit the offline queue and the sync
/// service operate on.
/// </summary>
public class Transaction
{
    #region Properties
    public Guid Id { get; set; }

    public Guid SaleId { get; set; }

    public Sale? Sale { get; set; }

    public PaymentMethod Method { get; set; }

    public decimal Amount { get; set; }

    public TransactionStatus Status { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? SyncedAt { get; set; }
    #endregion
}
