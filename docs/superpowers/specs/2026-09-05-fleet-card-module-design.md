# Fleet Card Module Design

**Owner:** Architect. **Status:** approved, not yet implemented.

## Goal

Replace the `FleetCardSale.razor` "coming soon" placeholder with a working tap-card payment
flow: an attendant enters an amount, taps a fleet card, the terminal authorizes it against a
simulated fleet-card backend, and on approval records a `Sale` + `Transaction` that the existing
offline sync queue picks up automatically.

## Why this module, why now

`Core/Modules/{FleetCard,MobileMoney,Cash}/README.md` are the three remaining unclaimed payment
modules. None of them has any Sale-creation infrastructure yet — `Sale`/`SaleLine` exist as
domain models, but there's no repository or service that persists them. FleetCard is the
first-named payment method in the target job's scope and has no network dependency (unlike
MobileMoney, which needs `ITransactionSyncService`), so it's the natural module to build first —
and the shared `Sale` persistence it introduces is deliberately generic so Cash and MobileMoney
can reuse it rather than each inventing their own.

## Architecture

New code, following the existing service-first pattern (`AttendantService`, `ShiftService`):

- **`Core/Sales/ISaleRepository`** + **`Core.Persistence/Repositories/EfSaleRepository`** —
  shared infra, not FleetCard-specific. `AddSaleWithTransactionAsync(Sale sale, Transaction
  transaction, CancellationToken)` persists a `Sale` (with its `SaleLine`s) and its paying
  `Transaction` in one atomic `SaveChangesAsync` against `TerminalDbContext`. This is the piece
  Cash and MobileMoney will also depend on when they're built.
- **`Core/FleetCard/IFleetCardAuthorizationService`** + **`SimulatedFleetCardAuthorizationService`**
  — shaped like `ICardReaderService`: an interface a real fleet-card backend adapter could
  implement later without callers changing. The simulator declines ~20% of authorizations via an
  injected `Random`, mirroring the existing `TimeProvider`/`FixedTimeProvider` pattern
  (`Random.NextDouble()` is virtual since .NET 6, so tests subclass it for a deterministic
  outcome instead of relying on a seed).
- **`Core/FleetCard/FleetCardSaleService`** — owns the whole orchestration. Razor pages stay
  dumb; this service does the work.
- **`MAUI-POS-DASH/Components/Pages/FleetCardSale.razor`** — amount input, calls
  `FleetCardSaleService.ProcessSaleAsync`, renders the result. Same session/shift guard pattern
  already used by `ShiftClose.razor`.

## Data flow

`FleetCardSaleService.ProcessSaleAsync(Guid shiftId, decimal amount, CancellationToken)`:

1. Validate `amount > 0` — throws `ArgumentException` otherwise (same convention as
   `AttendantService.ValidatePin`; the page catches it and shows the message). No card wait is
   triggered for an invalid amount.
2. `await _cardReader.WaitForCardAsync(cancellationToken)`. If `Status != CardReadStatus.Success`,
   return `FleetCardSaleResult(FleetCardSaleStatus.CardReadFailed, null, null, <reason>)` with
   nothing persisted.
3. `await _authorizationService.AuthorizeAsync(amount, cancellationToken)`. If declined, return
   `FleetCardSaleResult(FleetCardSaleStatus.Declined, null, null, "Card declined by the fleet
   operator.")` with nothing persisted.
4. On approval, build:
   - `Sale { Id = Guid.NewGuid(), ShiftId = shiftId, OccurredAt = DateTimeOffset.UtcNow, Lines =
     [SaleLine { Id = Guid.NewGuid(), Description = "Fuel", UnitPrice = amount, Quantity = 1 }] }`
   - `Transaction { Id = Guid.NewGuid(), SaleId = sale.Id, Method = PaymentMethod.FleetCard,
     Amount = amount, Status = TransactionStatus.Pending, CreatedAt = DateTimeOffset.UtcNow }`

   then `await _saleRepository.AddSaleWithTransactionAsync(sale, transaction, cancellationToken)`
   and return `FleetCardSaleResult(FleetCardSaleStatus.Approved, sale, transaction, "Payment
   approved.")`.

There's no product/pump catalog yet, so the Sale is always a single fixed `"Fuel"` line for the
entered amount rather than an itemized grade/quantity/price breakdown — that's a deliberate scope
cut, not an oversight, and matches how `ShiftOpen`/`ShiftClose` don't model real till hardware
either.

Because the Transaction is written with `Status = Pending`, `EfTransactionQueueStore` picks it up
on the next `OfflineTransactionQueue.TryFlushAsync()` automatically — no new sync wiring is
needed.

## UI states (`FleetCardSale.razor`)

- No signed-in attendant → redirect to `/login` (matches `ShiftClose.razor`).
- No open shift for the attendant → "There's no open shift — open one before taking payments."
  (matches `ShiftClose.razor`'s message pattern).
- Invalid amount (≤ 0) → inline error from the caught `ArgumentException`.
- `CardReadFailed` → show the reason, re-enable the form so the attendant can retry.
- `Declined` → show "Card declined by the fleet operator.", re-enable the form.
- `Approved` → show a confirmation with the amount and Sale ID; no auto-navigate, so the
  attendant sees confirmation before choosing to go back Home (same as
  `AttendantManagement.razor` not auto-navigating after a save).

## Error handling

Both new interfaces (`ICardReaderService`, `IFleetCardAuthorizationService`) fail by returning a
status/result value, not by throwing — matching how `ICardReaderService.WaitForCardAsync` already
signals `Timeout`/`DeviceUnavailable`/`Cancelled` via `CardReadResult` rather than exceptions.
`FleetCardSaleService` only throws for a genuine caller error (an invalid amount), not for
declined payments or hardware timeouts, since those are expected outcomes a real terminal must
handle gracefully, not exceptional program states.

## Testing

NUnit + seeded `TerminalDbContext` (SQLite in-memory), same shape as `AttendantServiceTests`:

- `EfSaleRepository` tests: `AddSaleWithTransactionAsync` persists both the Sale (with its line)
  and the Transaction; `Sale.Total`/`SaleLine.LineTotal` compute correctly from what was stored.
- `FleetCardSaleService` tests using hand-written fakes for `ICardReaderService` and
  `IFleetCardAuthorizationService` (fakes, not mocks — consistent with the project's "seeded real
  fixtures" preference; a fake avoids the real simulator's 2-second delay and keeps outcomes
  scriptable):
  - Approved → Sale + Transaction persisted with the right amount/status; result carries them.
  - Declined → nothing persisted; result is `Declined` with the decline reason.
  - Card read failure (`Timeout`, `DeviceUnavailable`, `Cancelled`) → nothing persisted; result is
    `CardReadFailed`.
  - Invalid amount (`<= 0`) → throws `ArgumentException`; nothing persisted, card reader never
    invoked.
- `SimulatedFleetCardAuthorizationService` test: inject a `Random` subclass forced to return
  values above/below the 0.2 decline threshold and assert Approved/Declined accordingly.

## Out of scope (explicitly deferred)

- Product/pump catalog and itemized fuel lines — single fixed `"Fuel"` line only, per the
  decision above.
- A real fleet-card backend integration — `IFleetCardAuthorizationService` is the seam a real
  adapter would implement later, matching `ICardReaderService`'s existing role for card hardware.
- Split-tender fleet card payments (multiple transactions against one Sale) — this module always
  creates exactly one Sale and one Transaction per successful tap.
