# Sale Sync Fix Design

**Owner:** Architect. **Status:** approved, not yet implemented.

## The bug

`TransactionConfiguration` gives `Transaction.SaleId` a real foreign-key constraint to `Sale` in
both `TerminalDbContext` and `BackofficeDbContext`. Nothing anywhere ever creates a `Sale` row in
`BackofficeDbContext` — `TransactionIngestionService` only *checks* whether the referenced Sale
already exists there; it never creates one. So every sync attempt from any terminal hits the
`MissingSale` (409) branch and fails, permanently. Pending transactions accumulate forever;
nothing ever reaches the backoffice. This is also why `Dashboard.razor` is still on sample data —
there has never been any real synced data to show.

This was flagged in `docs/ARCHITECTURE.md` as deferred "Sale/Shift graph synchronization" work,
but tracing the actual code turned it from a vague follow-up into a concretely verified defect:
the sync boundary, as shipped, cannot ever succeed.

## Scope

Fix Sale sync only — not Shift, not Attendant. The FK constraint that actually blocks every sync
is `Transaction → Sale`; Sale has no FK to Shift in the model, so Shift sync isn't required to
unblock this. Syncing Shift (and by extension Attendant, since `ShiftDto` flattens the
attendant's name) would drag in the still-open "terminal authentication" boundary named
separately in `docs/ARCHITECTURE.md`. Wiring `Dashboard.razor` to live data is a natural next
step after this lands, but is its own follow-up, not part of this fix.

## Architecture

- **`Core/Sync/TransactionSyncRequest`** (new) — `record TransactionSyncRequest(IReadOnlyList<SaleDto> Sales, IReadOnlyList<TransactionDto> Transactions)`.
  Minimal APIs bind one complex type from the JSON body; wrapping both lists in one request type
  is the minimal change needed to carry Sales alongside Transactions in a single POST, keeping the
  sync atomic (one HTTP round trip, one database transaction) rather than introducing a second
  network call and a parallel "pending sales" tracking concept the terminal doesn't otherwise
  need — every Sale synced today is created by exactly the payment module that also created its
  one Transaction, so they always travel together in practice.
- **`ITransactionIngestionService.IngestAsync`** signature grows to accept the Sales list
  alongside the existing Transactions list.
- **`TransactionIngestionService`** — upserts any Sales it doesn't already have, in the same
  tracked change set and the same `SaveChangesAsync`/`DbUpdateException` race-handling block it
  already uses for Transactions, before its existing missing-sale check runs. That check stays as
  a safety net: a Transaction whose Sale is neither pre-existing nor included in this request's
  `Sales` list is still rejected.
- **`TransactionsApi`** — the endpoint binds `TransactionSyncRequest` instead of a bare
  `List<TransactionDto>`.
- **`EfTransactionQueueStore.GetPendingAsync`** — gains `.Include(t => t.Sale!).ThenInclude(s => s.Lines)`
  so the Sale data is available to map without a second query.
- **`HttpTransactionSyncService`** — maps each pending transaction's already-loaded `.Sale` into a
  `SaleDto` (via the existing `EntityMapper.ToDto(Sale)`), de-duplicated by Sale ID (a future
  split-tender Sale could have more than one pending Transaction), and posts both lists together.

## Data flow

`TransactionIngestionService.IngestAsync(IReadOnlyList<SaleDto> sales, IReadOnlyList<TransactionDto> transactions, CancellationToken)`:

1. Existing `Validate(transactions)` — unchanged.
2. `requestedSaleIds` = distinct `SaleId`s from `transactions` — unchanged.
3. Query `_dbContext.Sales` for which of `requestedSaleIds` already exist — unchanged query, but
   the missing-sale check now treats a Sale ID as "known" if it's *either* already in the
   database *or* present in the incoming `sales` list, before deciding anything is missing.
4. For each `SaleDto` in `sales` whose ID isn't already in the database, build a `Sale` (+ its
   `SaleLine`s) and add it to the tracked change set (not saved yet).
5. Continue exactly as today: transaction-conflict check, build new `Transaction` entities,
   `SaveChangesAsync` inside the existing `try`/`catch (DbUpdateException)` — on failure, detach
   both the new Sales and the new Transactions (today only Transactions are detached), return
   `PersistenceConflict`.

`HttpTransactionSyncService.SyncAsync(pendingTransactions)`: build `sales` from
`pendingTransactions.Select(t => t.Sale).DistinctBy(s => s.Id).Select(_mapper.ToDto)`, build
`transactions` from `pendingTransactions.Select(_mapper.ToDto)` (unchanged), POST a single
`TransactionSyncRequest(sales, transactions)` to `api/transactions`.

## Error handling

No new failure modes. `MissingSale` still exists for the genuinely-malformed case (a Transaction
referencing a Sale that's neither stored nor included in the batch). The existing
`PersistenceConflict` race path now also covers a concurrent duplicate Sale insert, by extending
the same detach-and-retry behavior that already covers Transactions.

## Testing

Extends the existing `TransactionIngestionServiceTests` and `TransactionsApiTests` (real seeded
`BackofficeDbContext`, matching their current fixture style) rather than adding a new test class:

- A batch whose Sale doesn't exist in the backoffice yet, with that Sale included in the request's
  `Sales` list, now succeeds (previously would have been `MissingSale`) — the Sale and its Lines
  are persisted alongside the Transaction.
- A batch whose Sale already exists in the backoffice, with that same Sale also included in the
  request's `Sales` list (a retry), doesn't duplicate or error on the Sale — still succeeds as
  today's idempotent-retry Transaction path already does.
- A batch referencing a Sale that's neither pre-existing nor included in `Sales` still returns
  `MissingSale`, unchanged from today's behavior.
- `EfTransactionQueueStore.GetPendingAsync`'s new `Include(Sale).ThenInclude(Lines)` — this is the
  mechanism `HttpTransactionSyncService` depends on to have a Sale to map in the first place.

**Correction from the original version of this section:** it also called for a direct
`HttpTransactionSyncService` test. `HttpTransactionSyncService` lives in `MAUI-POS-DASH`
(`net10.0-android`) — there's no NUnit-testable desktop target framework for that project, unlike
`EfTransactionQueueStore` (in `Core.Persistence`, plain `net10.0`), so a real unit test for it
isn't feasible with this repo's current test infrastructure. QA flagged this as an unfulfilled
design promise; the `EfTransactionQueueStore` test above covers the testable half of the same
mechanism — the Sale-to-DTO mapping and de-duplication in `HttpTransactionSyncService` itself
remains manually-verified only.

**Second correction, added later:** `HttpTransactionSyncService` was subsequently moved to
`MAUI-POS-DASH.Core/Sync` so both terminal apps (`MAUI-POS-DASH` and the newer
`MAUI-POS-DASH.MAUI-Android`) could share one implementation instead of duplicating it. That move
also made it directly unit-testable in `MAUI-POS-DASH.Core.Tests` — the previously-deferred test
now exists at `MAUI-POS-DASH.Core.Tests/Sync/HttpTransactionSyncServiceTests.cs`, covering the
Sale-to-DTO mapping and de-duplication behavior the paragraph above says was manually-verified
only.

## Out of scope (explicitly deferred)

- Shift and Attendant sync.
- Wiring `Dashboard.razor` to live backoffice data — natural next step once this lands, but a
  separate task.
- Split-tender Sales (more than one Transaction per Sale) aren't exercised by any current payment
  module, but the de-duplication-by-Sale-ID in `HttpTransactionSyncService` means this fix doesn't
  block that future case either.
