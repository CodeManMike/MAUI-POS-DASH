namespace MAUI_POS_DASH.Web.Services;

/// <summary>
/// We own transaction-ingestion rules and persistence so the Minimal API remains an HTTP adapter.
/// </summary>
public class TransactionIngestionService : ITransactionIngestionService
{
    #region Fields
    private readonly BackofficeDbContext _dbContext;
    private readonly TimeProvider _timeProvider;
    #endregion

    #region Constructor
    /// <summary>We create the ingestion boundary over the scoped database and server clock.</summary>
    public TransactionIngestionService(BackofficeDbContext dbContext, TimeProvider timeProvider)
    {
        _dbContext = dbContext;
        _timeProvider = timeProvider;
    }
    #endregion

    #region Public Methods
    /// <inheritdoc />
    public async Task<TransactionIngestionResult> IngestAsync(
        IReadOnlyList<TransactionDto> transactions,
        CancellationToken cancellationToken = default)
    {
        DateTimeOffset serverTimestamp = _timeProvider.GetUtcNow();
        List<Transaction> entities = transactions.Select(transaction => new Transaction
        {
            Id = transaction.Id,
            SaleId = transaction.SaleId,
            Method = transaction.Method,
            Amount = transaction.Amount,
            Status = TransactionStatus.Synced,
            CreatedAt = transaction.CreatedAt.ToUniversalTime(),
            SyncedAt = serverTimestamp
        }).ToList();

        _dbContext.Transactions.AddRange(entities);
        await _dbContext.SaveChangesAsync(cancellationToken);

        TransactionIngestionItemResult[] items = entities
            .Select(entity => new TransactionIngestionItemResult(
                entity.Id,
                TransactionIngestionItemStatus.Inserted,
                entity.SyncedAt))
            .ToArray();

        return new TransactionIngestionResult(
            TransactionIngestionStatus.Success,
            items,
            []);
    }
    #endregion
}
