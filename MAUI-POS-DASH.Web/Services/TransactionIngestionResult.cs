namespace MAUI_POS_DASH.Web.Services;

/// <summary>We describe the overall outcome of one complete ingestion request.</summary>
public enum TransactionIngestionStatus
{
    /// <summary>Every transaction was inserted or already existed identically.</summary>
    Success,

    /// <summary>The request contains a malformed or internally duplicated transaction.</summary>
    InvalidRequest,

    /// <summary>At least one referenced Sale does not exist in the back-office store.</summary>
    MissingSale,

    /// <summary>An existing transaction ID has different terminal-owned values.</summary>
    TransactionConflict,

    /// <summary>The database rejected a write after preflight checks completed.</summary>
    PersistenceConflict
}

/// <summary>We classify what happened to one successfully accepted transaction.</summary>
public enum TransactionIngestionItemStatus
{
    /// <summary>The service inserted the transaction during this request.</summary>
    Inserted,

    /// <summary>The same transaction had already been accepted.</summary>
    AlreadySynced
}

/// <summary>We return the server-owned result for one accepted transaction.</summary>
/// <param name="TransactionId">The terminal-provided transaction identity.</param>
/// <param name="Status">Whether this request inserted the row or found an identical retry.</param>
/// <param name="SyncedAt">The authoritative timestamp currently stored by the server.</param>
public sealed record TransactionIngestionItemResult(
    Guid TransactionId,
    TransactionIngestionItemStatus Status,
    DateTimeOffset? SyncedAt);

/// <summary>We return one overall status plus item results or the IDs that blocked acceptance.</summary>
/// <param name="Status">The complete-batch outcome.</param>
/// <param name="Items">Per-item results when the batch succeeds.</param>
/// <param name="OffendingIds">Sale or transaction IDs responsible for a rejected batch.</param>
/// <param name="Detail">A safe explanation suitable for a Problem Details response.</param>
public sealed record TransactionIngestionResult(
    TransactionIngestionStatus Status,
    IReadOnlyList<TransactionIngestionItemResult> Items,
    IReadOnlyList<Guid> OffendingIds,
    string? Detail = null);
