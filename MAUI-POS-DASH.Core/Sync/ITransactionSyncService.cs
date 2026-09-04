namespace MAUI_POS_DASH.Core.Sync;

public enum SyncStatus
{
    Success,
    NetworkUnavailable,
    ServerRejected
}

public record SyncResult(SyncStatus Status, int TransactionsSynced);

/// <summary>
/// We keep this decoupled from HTTP on purpose — Core doesn't know or care that the real
/// implementation is an HttpClient call to the Web app's sync endpoint.
/// </summary>
public interface ITransactionSyncService
{
    Task<SyncResult> SyncAsync(IReadOnlyList<Transaction> pendingTransactions, CancellationToken cancellationToken = default);
}
