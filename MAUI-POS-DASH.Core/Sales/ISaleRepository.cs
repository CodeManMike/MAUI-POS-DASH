namespace MAUI_POS_DASH.Core.Sales;

/// <summary>
/// We keep Sale/Transaction persistence behind this interface so payment modules (FleetCard,
/// Cash, MobileMoney) don't touch TerminalDbContext directly — every module records a completed
/// tender the same way: one Sale, one Transaction, written together.
/// </summary>
public interface ISaleRepository
{
    Task AddSaleWithTransactionAsync(Sale sale, Transaction transaction, CancellationToken cancellationToken = default);
}
