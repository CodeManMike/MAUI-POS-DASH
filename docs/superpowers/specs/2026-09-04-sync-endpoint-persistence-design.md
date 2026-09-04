# Sync Endpoint Persistence — Design

**Date:** 2026-09-04
**Status:** Approved
**Author:** Builder (Codex), in collaboration with Overmind and Architect

## 1. Purpose

This work replaces `POST /api/transactions`'s `501 Not Implemented` stub with durable,
idempotent transaction ingestion into `BackofficeDbContext`. It claims the Sync endpoint
persistence row in `docs/ARCHITECTURE.md` without overlapping Architect's Attendant Management
module.

The current terminal sends `List<TransactionDto>` and only distinguishes a successful HTTP
status from a rejected request. We retain that wire request so the existing MAUI client continues
to work unchanged, while returning a structured success body that future clients can consume.

## 2. Scope decisions

- **Use a service layer.** `TransactionsApi` owns routing and HTTP result mapping only.
  `TransactionIngestionService` owns validation, lookup, idempotency, server authority, and EF
  Core persistence.
- **Keep the existing request contract.** The endpoint continues to receive
  `List<TransactionDto>`; no Core contract or MAUI client change is required.
- **Require pre-existing sales.** Every `TransactionDto.SaleId` must already exist in the
  back-office database. If any sale is missing, the complete batch is rejected with no writes.
- **Use server-authoritative sync state.** New rows are stored with
  `TransactionStatus.Synced` and one server UTC timestamp for the batch. Client-provided
  `Status` and `SyncedAt` values are ignored.
- **Make retries idempotent.** An existing transaction with the same ID and immutable terminal
  fields is returned as `AlreadySynced` without another insert. Reusing an ID with different
  immutable fields rejects the complete batch.
- **Keep writes all-or-nothing.** Validation and dependency/conflict checks complete before any
  entities are added. All new rows are persisted in one `SaveChangesAsync` call, which EF Core
  wraps transactionally.
- **Defer sale transport and authentication.** The existing transaction-only request cannot
  create its required Sale/Shift graph, and the solution has no machine-authentication contract.
  Sale synchronization and endpoint authentication remain explicit follow-up work rather than
  being improvised across Architect-owned Core and MAUI files.

## 3. Components and boundaries

### `ITransactionIngestionService`

The Web-local application boundary exposes one operation:

```csharp
Task<TransactionIngestionResult> IngestAsync(
    IReadOnlyList<TransactionDto> transactions,
    CancellationToken cancellationToken = default);
```

It is registered as scoped because it depends on the scoped `BackofficeDbContext`.

### `TransactionIngestionService`

The implementation depends on `BackofficeDbContext` and `TimeProvider`. It performs all
application behavior:

1. Validate the complete request.
2. Load referenced Sales in one query.
3. Load existing Transactions in one query.
4. Detect missing dependencies and transaction-ID conflicts.
5. Classify identical retries as `AlreadySynced`.
6. Map and add only new Transactions, applying server-owned status and timestamp fields.
7. Save once and return one result per unique request item.

`TimeProvider.System` is registered in Web DI. Tests supply a fixed provider without adding a
clock package.

### Result types

`TransactionIngestionResult` carries an overall status, item results, and offending IDs when a
request is rejected. Its status values are:

- `Success`
- `InvalidRequest`
- `MissingSale`
- `TransactionConflict`
- `PersistenceConflict`

Each successful `TransactionIngestionItemResult` contains the transaction ID, an item status of
`Inserted` or `AlreadySynced`, and the authoritative `SyncedAt` timestamp stored for that row.

The result types live in the Web service layer. They do not expand Core's public wire contract,
and the current MAUI client is free to continue ignoring the response body.

### `TransactionsApi`

The Minimal API handler injects `ITransactionIngestionService`, calls `IngestAsync`, and maps its
result to HTTP. It contains no EF Core query, mapping, validation, or business-rule logic.

### `MAUI-POS-DASH.Web.Tests`

A dedicated NUnit project references Web and Core.Persistence. Service tests use SQLite in memory
with `BackofficeDbContext.Database.EnsureCreated()`, seeding real Attendant, Shift, and Sale
relationships in `[SetUp]` and disposing the database in `[TearDown]`.

HTTP-level tests use ASP.NET Core's test host to verify the route's observable status, content
type, and response body. This requires the standard `Microsoft.AspNetCore.Mvc.Testing` test-only
package and a public partial `Program` entry point. Production projects gain no new package.

## 4. Validation

The service rejects the complete batch as `InvalidRequest` before querying or writing when any
item has:

- `Id == Guid.Empty`
- `SaleId == Guid.Empty`
- an undefined `PaymentMethod`
- `Amount <= 0`
- `CreatedAt == default`
- a transaction ID repeated within the same request

An empty list is valid and returns `Success` with zero item results. This keeps the operation
mathematically idempotent and avoids inventing an error for a harmless no-op.

`CreatedAt` is normalized with `ToUniversalTime()` before persistence so Npgsql receives a UTC
value. The original instant remains unchanged.

## 5. Idempotency and conflict identity

For an existing transaction ID, the service compares the fields owned by the terminal:

- `SaleId`
- `Method`
- `Amount`
- the UTC instant represented by `CreatedAt`

It deliberately does not compare `Status` or `SyncedAt`, because the back office owns both.

If all immutable fields match, the retry succeeds as `AlreadySynced` and reports the existing
server timestamp. If any immutable field differs, the batch returns `TransactionConflict` and
nothing is written.

A database uniqueness race can still occur after the preflight query. `DbUpdateException` is
caught at the service boundary, tracked additions are detached, and the service returns
`PersistenceConflict`; it does not leak provider exception details through HTTP.

## 6. HTTP behavior

| Service outcome | HTTP response | Body |
|---|---|---|
| `Success` | `200 OK` | JSON array of per-transaction results |
| `InvalidRequest` | `400 Bad Request` | `application/problem+json` with a safe validation detail |
| `MissingSale` | `409 Conflict` | `application/problem+json` listing missing Sale IDs |
| `TransactionConflict` | `409 Conflict` | `application/problem+json` listing conflicting Transaction IDs |
| `PersistenceConflict` | `409 Conflict` | `application/problem+json` with a retry-safe conflict detail |

Every error result has a body. The endpoint must never return a bare status code because
`UseStatusCodePagesWithReExecute` would otherwise replay the original JSON POST against the Razor
`/not-found` endpoint.

Antiforgery remains disabled for this machine-to-machine endpoint. This is not authentication;
the endpoint must not be treated as Internet-ready until the follow-up authentication design is
implemented.

## 7. Data flow

```text
MAUI OfflineTransactionQueue
    -> HttpTransactionSyncService
    -> POST List<TransactionDto>
    -> TransactionsApi (HTTP only)
    -> ITransactionIngestionService
    -> validate complete batch
    -> query Sales and existing Transactions
    -> reject complete batch, or classify new/retry items
    -> one EF Core SaveChangesAsync
    -> per-item success results
    -> MAUI sees 2xx and marks its local pending rows synced
```

Because the current MAUI client marks every submitted local transaction as synced after any 2xx,
the Web service returns success only when every request item is either newly inserted or already
present identically. Partial acceptance is not allowed.

## 8. Testing

The service suite covers:

- a new transaction is persisted with `Synced` status and the fixed server UTC timestamp;
- client-provided `Status` and `SyncedAt` are ignored;
- multiple new transactions are saved together;
- an identical retry returns `AlreadySynced` without adding a duplicate;
- a mixed new/identical-retry batch succeeds with the correct item results;
- a reused ID with different immutable data rejects the complete batch;
- any missing Sale rejects the complete batch;
- each invalid field and an in-batch duplicate ID reject without writes;
- an empty batch succeeds with zero results;
- cancellation is propagated to EF Core operations.

The HTTP suite covers:

- `200 OK` and the success response shape;
- `400 Bad Request` with `application/problem+json`;
- each `409 Conflict` path with `application/problem+json`;
- the existing route still accepts the current JSON `List<TransactionDto>` payload;
- the status-code-page middleware does not re-execute body-bearing error responses.

Final verification runs the targeted Web tests, the existing Core tests, the full solution build,
and a live local POST smoke. If a disposable Postgres instance is reachable, the live smoke also
uses the real Backoffice provider; otherwise the provider-specific live step is reported as
skipped rather than inferred from SQLite results.

## 9. Files and non-overlap

Expected Builder-owned changes:

- `AGENTS.md` — already records the global service-first/dumb-UI rule.
- `docs/ARCHITECTURE.md` — claims the Sync endpoint row.
- `MAUI-POS-DASH.Web/Api/TransactionsApi.cs` — thin HTTP mapping.
- `MAUI-POS-DASH.Web/Services/*` — interface, implementation, and result types.
- `MAUI-POS-DASH.Web/Program.cs` — service and `TimeProvider` registration plus testable entry
  point.
- `MAUI-POS-DASH.Web.Tests/*` — NUnit service and endpoint tests.
- `MAUI-POS-DASH.slnx` — includes the Web test project.
- this design specification and its implementation plan.

This pass does not modify `MAUI-POS-DASH.Core`, `MAUI-POS-DASH.Core.Persistence`,
`MAUI-POS-DASH.Core.Tests`, the MAUI terminal project, migrations, or Architect's Attendant
Management files.

## 10. Follow-up work

Two independent designs must follow before this becomes a complete production sync boundary:

1. Sync the Sale/Shift graph before its Transactions, or introduce an aggregate sync contract
   that transports the complete graph.
2. Authenticate terminals and authorize transaction ingestion, including credential provisioning,
   rotation, revocation, and per-terminal audit identity.
