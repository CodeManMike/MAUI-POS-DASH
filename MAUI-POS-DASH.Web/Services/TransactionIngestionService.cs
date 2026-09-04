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
        TransactionIngestionResult? validationFailure = Validate(transactions);
        if (validationFailure is not null)
        {
            return validationFailure;
        }

        if (transactions.Count == 0)
        {
            return Success([]);
        }

        Guid[] requestedSaleIds = transactions
            .Select(transaction => transaction.SaleId)
            .Distinct()
            .ToArray();
        Guid[] existingSaleIds = await _dbContext.Sales
            .Where(sale => requestedSaleIds.Contains(sale.Id))
            .Select(sale => sale.Id)
            .ToArrayAsync(cancellationToken);
        Guid[] missingSaleIds = requestedSaleIds
            .Except(existingSaleIds)
            .Order()
            .ToArray();

        if (missingSaleIds.Length > 0)
        {
            return Failure(
                TransactionIngestionStatus.MissingSale,
                missingSaleIds,
                "One or more referenced sales do not exist.");
        }

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

        return Success(entities.Select(entity => new TransactionIngestionItemResult(
            entity.Id,
            TransactionIngestionItemStatus.Inserted,
            entity.SyncedAt)).ToArray());
    }
    #endregion

    #region Private Methods
    private static TransactionIngestionResult? Validate(IReadOnlyList<TransactionDto> transactions)
    {
        HashSet<Guid> transactionIds = [];

        for (int index = 0; index < transactions.Count; index++)
        {
            TransactionDto transaction = transactions[index];
            string? detail = transaction switch
            {
                { Id: var id } when id == Guid.Empty => $"Transaction at index {index} must have a non-empty ID.",
                { SaleId: var saleId } when saleId == Guid.Empty => $"Transaction at index {index} must reference a Sale.",
                { Method: var method } when !Enum.IsDefined(method) => $"Transaction at index {index} has an unsupported payment method.",
                { Amount: <= 0 } => $"Transaction at index {index} must have a positive amount.",
                { CreatedAt: var createdAt } when createdAt == default => $"Transaction at index {index} must have a creation timestamp.",
                _ => null
            };

            if (detail is not null)
            {
                return Failure(TransactionIngestionStatus.InvalidRequest, [transaction.Id], detail);
            }

            if (!transactionIds.Add(transaction.Id))
            {
                return Failure(
                    TransactionIngestionStatus.InvalidRequest,
                    [transaction.Id],
                    $"Transaction {transaction.Id} appears more than once in this request.");
            }
        }

        return null;
    }

    private static TransactionIngestionResult Success(
        IReadOnlyList<TransactionIngestionItemResult> items)
    {
        return new TransactionIngestionResult(TransactionIngestionStatus.Success, items, []);
    }

    private static TransactionIngestionResult Failure(
        TransactionIngestionStatus status,
        IReadOnlyList<Guid> offendingIds,
        string detail)
    {
        return new TransactionIngestionResult(status, [], offendingIds, detail);
    }
    #endregion
}
