namespace MAUI_POS_DASH.Web.Services;

/// <summary>
/// We own transaction-ingestion rules and persistence so the Minimal API remains an HTTP adapter.
/// </summary>
public class TransactionIngestionService : ITransactionIngestionService
{
    #region Fields
    // numeric(18,2) allows 16 integer digits and 2 fractional digits — this is the largest value
    // that column can hold without PostgreSQL rounding or overflowing it on insert.
    private const decimal MaxStorableAmount = 9_999_999_999_999_999.99m;

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
        IReadOnlyList<SaleDto> sales,
        IReadOnlyList<TransactionDto> transactions,
        CancellationToken cancellationToken = default)
    {
        TransactionIngestionResult? salesValidationFailure = ValidateSales(sales);
        if (salesValidationFailure is not null)
        {
            return salesValidationFailure;
        }

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
        var existingSaleIdSet = existingSaleIds.ToHashSet();

        // A requested Sale ID is "known" if it's already stored OR included in this request's Sales —
        // that's what actually fixes MissingSale firing on every first-time sync.
        HashSet<Guid> knownSaleIds = [.. existingSaleIdSet, .. sales.Select(sale => sale.Id)];
        Guid[] missingSaleIds = requestedSaleIds
            .Except(knownSaleIds)
            .Order()
            .ToArray();

        if (missingSaleIds.Length > 0)
        {
            return Failure(
                TransactionIngestionStatus.MissingSale,
                missingSaleIds,
                "One or more referenced sales do not exist.");
        }

        List<Sale> newSales = sales
            .Where(sale => !existingSaleIdSet.Contains(sale.Id))
            .Select(sale => new Sale
            {
                Id = sale.Id,
                ShiftId = sale.ShiftId,
                OccurredAt = sale.OccurredAt,
                Lines = sale.Lines.Select(line => new SaleLine
                {
                    Id = line.Id,
                    Description = line.Description,
                    UnitPrice = line.UnitPrice,
                    Quantity = line.Quantity
                }).ToList()
            })
            .ToList();

        Guid[] requestedTransactionIds = transactions
            .Select(transaction => transaction.Id)
            .ToArray();
        Dictionary<Guid, Transaction> existingTransactions = await _dbContext.Transactions
            .Where(transaction => requestedTransactionIds.Contains(transaction.Id))
            .ToDictionaryAsync(transaction => transaction.Id, cancellationToken);
        Guid[] conflictingTransactionIds = transactions
            .Where(transaction => existingTransactions.TryGetValue(transaction.Id, out Transaction? existing)
                && !MatchesImmutableFields(transaction, existing))
            .Select(transaction => transaction.Id)
            .Order()
            .ToArray();

        if (conflictingTransactionIds.Length > 0)
        {
            return Failure(
                TransactionIngestionStatus.TransactionConflict,
                conflictingTransactionIds,
                "One or more transaction IDs already contain different values.");
        }

        DateTimeOffset serverTimestamp = _timeProvider.GetUtcNow();
        List<Transaction> newEntities = [];
        List<TransactionIngestionItemResult> itemResults = new(transactions.Count);

        foreach (TransactionDto transaction in transactions)
        {
            if (existingTransactions.TryGetValue(transaction.Id, out Transaction? existing))
            {
                itemResults.Add(new TransactionIngestionItemResult(
                    transaction.Id,
                    TransactionIngestionItemStatus.AlreadySynced,
                    existing.SyncedAt));
                continue;
            }

            var entity = new Transaction
            {
                Id = transaction.Id,
                SaleId = transaction.SaleId,
                Method = transaction.Method,
                Amount = transaction.Amount,
                Status = TransactionStatus.Synced,
                CreatedAt = transaction.CreatedAt.ToUniversalTime(),
                SyncedAt = serverTimestamp
            };
            newEntities.Add(entity);
            itemResults.Add(new TransactionIngestionItemResult(
                entity.Id,
                TransactionIngestionItemStatus.Inserted,
                entity.SyncedAt));
        }

        if (newEntities.Count > 0 || newSales.Count > 0)
        {
            _dbContext.Sales.AddRange(newSales);
            _dbContext.Transactions.AddRange(newEntities);

            try
            {
                await _dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException)
            {
                foreach (Sale sale in newSales)
                {
                    _dbContext.Entry(sale).State = EntityState.Detached;
                }

                foreach (Transaction entity in newEntities)
                {
                    _dbContext.Entry(entity).State = EntityState.Detached;
                }

                return Failure(
                    TransactionIngestionStatus.PersistenceConflict,
                    newEntities.Select(entity => entity.Id).Order().ToArray(),
                    "The database changed while this batch was being accepted. Retry the complete batch.");
            }
        }

        return Success(itemResults);
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// We check this separately from Validate(transactions) — without it, two SaleDto entries
    /// sharing an Id that isn't already stored both land in the same AddRange call below, and EF's
    /// change tracker throws a raw InvalidOperationException for the duplicate key, bypassing every
    /// other failure path's ProblemDetails contract.
    /// </summary>
    private static TransactionIngestionResult? ValidateSales(IReadOnlyList<SaleDto> sales)
    {
        HashSet<Guid> saleIds = [];

        for (int index = 0; index < sales.Count; index++)
        {
            SaleDto? sale = sales[index];

            if (sale is null)
            {
                return Failure(
                    TransactionIngestionStatus.InvalidRequest,
                    [],
                    $"Sale at index {index} must not be null.");
            }

            if (sale.Id == Guid.Empty)
            {
                return Failure(
                    TransactionIngestionStatus.InvalidRequest,
                    [],
                    $"Sale at index {index} must have a non-empty ID.");
            }

            if (!saleIds.Add(sale.Id))
            {
                return Failure(
                    TransactionIngestionStatus.InvalidRequest,
                    [sale.Id],
                    $"Sale {sale.Id} appears more than once in this request.");
            }
        }

        return null;
    }

    private static TransactionIngestionResult? Validate(IReadOnlyList<TransactionDto> transactions)
    {
        HashSet<Guid> transactionIds = [];

        for (int index = 0; index < transactions.Count; index++)
        {
            TransactionDto? transaction = transactions[index];

            if (transaction is null)
            {
                return Failure(
                    TransactionIngestionStatus.InvalidRequest,
                    [],
                    $"Transaction at index {index} must not be null.");
            }

            string? detail = transaction switch
            {
                { Id: var id } when id == Guid.Empty => $"Transaction at index {index} must have a non-empty ID.",
                { SaleId: var saleId } when saleId == Guid.Empty => $"Transaction at index {index} must reference a Sale.",
                { Method: var method } when !Enum.IsDefined(method) => $"Transaction at index {index} has an unsupported payment method.",
                { Amount: <= 0 } => $"Transaction at index {index} must have a positive amount.",
                // We reject anything that can't round-trip through the numeric(18,2) Amount column
                // exactly — otherwise Postgres silently rounds it on insert, and a lost-acknowledgement
                // retry then compares the terminal's original (unrounded) DTO against the rounded value
                // already stored, turning a should-be-idempotent retry into a permanent conflict.
                { Amount: var amount } when !CanRoundTripThroughCurrencyColumn(amount) =>
                    $"Transaction at index {index} has an amount that can't be stored exactly as currency (at most two decimal places, up to {MaxStorableAmount:N2}).",
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

    private static bool CanRoundTripThroughCurrencyColumn(decimal amount)
    {
        return amount <= MaxStorableAmount && decimal.Round(amount, 2) == amount;
    }

    private static bool MatchesImmutableFields(TransactionDto requested, Transaction existing)
    {
        return requested.SaleId == existing.SaleId
            && requested.Method == existing.Method
            && requested.Amount == existing.Amount
            && requested.CreatedAt.ToUniversalTime() == existing.CreatedAt.ToUniversalTime();
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
