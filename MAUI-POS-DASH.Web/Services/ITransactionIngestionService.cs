namespace MAUI_POS_DASH.Web.Services;

/// <summary>
/// We accept terminal transaction batches into the back-office store without exposing EF Core to
/// the HTTP endpoint.
/// </summary>
public interface ITransactionIngestionService
{
    /// <summary>
    /// We validate and atomically ingest one complete batch, treating identical retries as
    /// successful no-ops.
    /// </summary>
    Task<TransactionIngestionResult> IngestAsync(
        IReadOnlyList<TransactionDto> transactions,
        CancellationToken cancellationToken = default);
}
