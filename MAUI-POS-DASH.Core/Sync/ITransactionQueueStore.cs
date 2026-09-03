namespace MAUI_POS_DASH.Core.Sync;

/// <summary>
/// We give the offline queue somewhere to read and update pending transactions without knowing
/// whether that storage is SQLite, Postgres, or something else — Core.Persistence supplies the
/// real implementation.
/// </summary>
public interface ITransactionQueueStore
{
    Task<IReadOnlyList<Transaction>> GetPendingAsync(CancellationToken cancellationToken = default);

    Task MarkSyncedAsync(IReadOnlyList<Guid> transactionIds, CancellationToken cancellationToken = default);
}
