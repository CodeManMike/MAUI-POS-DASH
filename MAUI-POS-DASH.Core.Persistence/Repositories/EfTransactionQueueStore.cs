using MAUI_POS_DASH.Core.Sync;

namespace MAUI_POS_DASH.Core.Persistence.Repositories;

/// <summary>
/// We implement ITransactionQueueStore against TerminalDbContext so OfflineTransactionQueue has
/// somewhere real to read pending transactions from and mark them synced.
/// </summary>
public class EfTransactionQueueStore : ITransactionQueueStore
{
    #region Fields
    private readonly TerminalDbContext _dbContext;
    #endregion

    #region Constructor
    public EfTransactionQueueStore(TerminalDbContext dbContext)
    {
        _dbContext = dbContext;
    }
    #endregion

    #region Public Methods
    public async Task<IReadOnlyList<Transaction>> GetPendingAsync(CancellationToken cancellationToken = default)
    {
        // List<T> implicitly satisfies IReadOnlyList<T> as a return value, so this awaits and
        // returns directly with no extra casting. We eager-load the Sale and its Lines here so
        // HttpTransactionSyncService can build the Sales half of the sync payload without a
        // second round trip.
        return await _dbContext.Transactions
            .Where(transaction => transaction.Status == TransactionStatus.Pending)
            .Include(transaction => transaction.Sale!)
                .ThenInclude(sale => sale.Lines)
            .ToListAsync(cancellationToken);
    }

    public async Task MarkSyncedAsync(IReadOnlyList<Guid> transactionIds, CancellationToken cancellationToken = default)
    {
        var toUpdate = await _dbContext.Transactions
            .Where(transaction => transactionIds.Contains(transaction.Id))
            .ToListAsync(cancellationToken);

        foreach (var transaction in toUpdate)
        {
            transaction.Status = TransactionStatus.Synced;
            transaction.SyncedAt = DateTimeOffset.UtcNow;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }
    #endregion
}
