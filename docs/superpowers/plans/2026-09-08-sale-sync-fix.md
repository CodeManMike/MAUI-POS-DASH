# Sale Sync Fix Implementation Plan

**Goal:** Fix the broken sync boundary — every sync attempt currently fails with `MissingSale`
because nothing ever creates a `Sale` in `BackofficeDbContext` — per
`docs/superpowers/specs/2026-09-08-sale-sync-fix-design.md`.

---

## Task 1: TransactionSyncRequest and the ingestion service signature

**Files:**
- Create: `MAUI-POS-DASH.Core/Sync/TransactionSyncRequest.cs`
- Modify: `MAUI-POS-DASH.Web/Services/ITransactionIngestionService.cs`
- Modify: `MAUI-POS-DASH.Web/Services/TransactionIngestionService.cs`
- Modify: `MAUI-POS-DASH.Web.Tests/Services/TransactionIngestionServiceTests.cs`

```csharp
// TransactionSyncRequest.cs
namespace MAUI_POS_DASH.Core.Sync;

/// <summary>
/// We wrap both lists in one request type because Minimal API model binding only binds one
/// complex type from the JSON body — Sales and Transactions need to travel in a single request so
/// the sync stays one HTTP round trip and one atomic database write.
/// </summary>
public sealed record TransactionSyncRequest(IReadOnlyList<SaleDto> Sales, IReadOnlyList<TransactionDto> Transactions);
```

`ITransactionIngestionService.IngestAsync` gains a `sales` parameter:
```csharp
Task<TransactionIngestionResult> IngestAsync(
    IReadOnlyList<SaleDto> sales,
    IReadOnlyList<TransactionDto> transactions,
    CancellationToken cancellationToken = default);
```

`TransactionIngestionService.IngestAsync` changes:
```csharp
public async Task<TransactionIngestionResult> IngestAsync(
    IReadOnlyList<SaleDto> sales,
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

    // ... existing transaction-id/conflict logic is unchanged from here ...

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
```

Note the `if (newEntities.Count > 0 || newSales.Count > 0)` guard changes from the original
`if (newEntities.Count > 0)` — a request with only already-synced (retry) transactions but a new
Sale attached should still persist that Sale. In practice this is rare (the Sale is normally new
exactly when its Transaction is new), but the guard should reflect the real condition rather than
assume they always move together.

- [ ] Update `TransactionIngestionServiceTests`: change every `_sut.IngestAsync([...])` call site
      to `_sut.IngestAsync([], [...])` (empty Sales list) to keep existing tests compiling and
      passing unchanged
- [ ] Write new tests: a Sale included in `sales` and not yet in the backoffice succeeds (Sale +
      Lines + Transaction all persisted); the same Sale included again on a retry doesn't
      duplicate or error; a Transaction whose Sale is in neither the database nor `sales` still
      returns `MissingSale`
- [ ] Run new tests, confirm they fail (behavior not yet implemented / signature not yet changed)
- [ ] Apply the `TransactionIngestionService` and interface changes above
- [ ] Run the full `TransactionIngestionServiceTests` file, confirm all pass
- [ ] Commit: "Make TransactionIngestionService accept and persist Sales"

## Task 2: Wire the endpoint

**Files:**
- Modify: `MAUI-POS-DASH.Web/Api/TransactionsApi.cs`
- Modify: `MAUI-POS-DASH.Web.Tests/Api/TransactionsApiTests.cs`

```csharp
private static async Task<IResult> IngestTransactionsAsync(
    TransactionSyncRequest request,
    ITransactionIngestionService ingestionService,
    CancellationToken cancellationToken)
{
    TransactionIngestionResult result =
        await ingestionService.IngestAsync(request.Sales, request.Transactions, cancellationToken);

    // ... switch expression body unchanged ...
}
```

- [ ] Update `TransactionsApiTests`: change every POST body from a bare transaction list to
      `new TransactionSyncRequest(sales, transactions)`, adding the Sale to `sales` wherever a
      test currently pre-seeds one directly into the test `BackofficeDbContext` instead
- [ ] Run, confirm failure (signature/body shape mismatch)
- [ ] Apply the endpoint change
- [ ] Run, confirm all `TransactionsApiTests` pass
- [ ] Commit: "Bind TransactionSyncRequest in the transactions endpoint"

## Task 3: Terminal-side — load and send Sales

**Files:**
- Modify: `MAUI-POS-DASH.Core.Persistence/Repositories/EfTransactionQueueStore.cs`
- Modify: `MAUI-POS-DASH/Services/HttpTransactionSyncService.cs`

```csharp
// EfTransactionQueueStore.GetPendingAsync
public async Task<IReadOnlyList<Transaction>> GetPendingAsync(CancellationToken cancellationToken = default)
{
    return await _dbContext.Transactions
        .Where(transaction => transaction.Status == TransactionStatus.Pending)
        .Include(transaction => transaction.Sale!)
            .ThenInclude(sale => sale.Lines)
        .ToListAsync(cancellationToken);
}
```

```csharp
// HttpTransactionSyncService.SyncAsync
public async Task<SyncResult> SyncAsync(IReadOnlyList<Transaction> pendingTransactions, CancellationToken cancellationToken = default)
{
    var sales = pendingTransactions
        .Select(transaction => transaction.Sale)
        .Where(sale => sale is not null)
        .DistinctBy(sale => sale!.Id)
        .Select(sale => _mapper.ToDto(sale!))
        .ToList();
    var transactions = pendingTransactions.Select(_mapper.ToDto).ToList();
    var request = new TransactionSyncRequest(sales, transactions);

    try
    {
        var response = await _httpClient.PostAsJsonAsync("api/transactions", request, cancellationToken);
        return response.IsSuccessStatusCode
            ? new SyncResult(SyncStatus.Success, transactions.Count)
            : new SyncResult(SyncStatus.ServerRejected, 0);
    }
    catch (HttpRequestException)
    {
        return new SyncResult(SyncStatus.NetworkUnavailable, 0);
    }
}
```

No existing unit tests cover `EfTransactionQueueStore` or `HttpTransactionSyncService` directly
(checked — neither has a test file today), so this task adds none rather than inventing a test
style inconsistent with how the rest of the sync path is covered (`TransactionIngestionServiceTests`
and `TransactionsApiTests` already exercise the ingestion side end-to-end with real data).

- [ ] Apply the `EfTransactionQueueStore` and `HttpTransactionSyncService` changes above
- [ ] Build the MAUI Android target, confirm 0 warnings/0 errors
- [ ] Commit: "Send Sales alongside Transactions from the terminal sync client"

## Task 4: Full verification and PR

- [ ] `dotnet test MAUI-POS-DASH.Core.Tests` and `MAUI-POS-DASH.Web.Tests` — confirm all pass, no
      regressions
- [ ] `dotnet build MAUI-POS-DASH.slnx` — confirm 0 warnings/0 errors including Android
- [ ] Update `docs/ARCHITECTURE.md`'s ownership row to `Builder, Sale-sync fix by Architect (done)`
      and remove the now-stale "Sale/Shift graph synchronization... follow-up work" line from the
      placeholder section (Shift sync itself is still explicitly out of scope and stays noted)
- [ ] Push `task/07-sale-sync-fix`, open PR
