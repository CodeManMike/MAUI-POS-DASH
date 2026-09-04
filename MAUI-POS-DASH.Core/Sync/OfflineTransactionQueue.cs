namespace MAUI_POS_DASH.Core.Sync;

/// <summary>
/// We hold transactions that have been saved locally but not yet confirmed by the back office,
/// and periodically try to flush them once connectivity allows.
/// </summary>
public class OfflineTransactionQueue
{
    #region Fields
    private readonly ITransactionQueueStore _store;
    private readonly ITransactionSyncService _syncService;
    #endregion

    #region Constructor
    public OfflineTransactionQueue(ITransactionQueueStore store, ITransactionSyncService syncService)
    {
        _store = store;
        _syncService = syncService;
    }
    #endregion

    #region Public Methods
    /// <summary>
    /// We attempt to flush every pending transaction to the back office. A sale is never blocked
    /// on this running — by the time it's called, the sale is already safely on local disk.
    /// </summary>
    public async Task<SyncResult> TryFlushAsync(CancellationToken cancellationToken = default)
    {
        var pending = await _store.GetPendingAsync(cancellationToken);
        if (pending.Count == 0)
        {
            return new SyncResult(SyncStatus.Success, 0);
        }

        var result = await _syncService.SyncAsync(pending, cancellationToken);
        if (result.Status == SyncStatus.Success)
        {
            await _store.MarkSyncedAsync(pending.Select(transaction => transaction.Id).ToList(), cancellationToken);
        }

        return result;
    }
    #endregion
}
