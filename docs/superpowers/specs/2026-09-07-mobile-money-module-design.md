# Mobile Money Module Design

**Owner:** Architect. **Status:** approved, not yet implemented.

## Goal

Replace the `MobileMoneySale.razor` "coming soon" placeholder with a working mobile money
payment flow: the attendant enters an amount, a simulated request goes out for the customer to
confirm on their phone, and on confirmation the terminal records a `Sale` + `Transaction` and
feeds `Till.MobileMoneyTotal` — the last of the three payment modules.

## Correcting a stale README note

The module's README previously said this depends on `Core/Sync/ITransactionSyncService.cs`
because "mobile money confirmation is inherently a network-round-trip." That's true in spirit but
wrong in mechanism: `ITransactionSyncService.SyncAsync(pendingTransactions)` is the
Terminal→Backoffice batch sync built in PR #3 — it syncs already-created, locally-Pending
transactions to Postgres. It has nothing to do with confirming a single payment is still in
flight with an external provider. The right analog, confirmed with the user, is FleetCard's
`IFleetCardAuthorizationService`: a purpose-built interface a real mobile money provider adapter
could implement later, with a simulated implementation standing in for now. The README has been
corrected to reflect this.

## Why now, and what's different from FleetCard

MobileMoney is the last unclaimed payment module. Unlike FleetCard (built before Cash introduced
the Till lifecycle) and unlike Cash's `FleetCardTotal`/`MobileMoneyTotal` gap noted in its own
README, MobileMoney is built *after* `ITillRepository` exists — so it feeds
`Till.MobileMoneyTotal` from day one, the same way `CashSaleService` feeds `CashTotal`. The
FleetCard retrofit (making it feed `FleetCardTotal` too) is separate follow-up work, explicitly
deferred until after this module ships.

## Architecture

- **`Core/MobileMoney/IMobileMoneyPaymentService`** + **`SimulatedMobileMoneyPaymentService`** —
  shaped after `IFleetCardAuthorizationService`. `RequestPaymentAsync(decimal amount)` returns a
  `MobileMoneyPaymentResult` with one of three outcomes — **Confirmed** (65%), **Declined** (20%),
  **TimedOut** (15%) — rolled via an injected `Random`, same testable pattern as FleetCard's
  decline chance. It also takes an injected `TimeSpan confirmationDelay`: DI registers roughly 4
  seconds for the real app (a customer checking their phone and confirming genuinely takes longer
  than a card tap), while tests pass `TimeSpan.Zero` so the suite stays fast.
- **`Core/MobileMoney/MobileMoneySaleService`** — mirrors `FleetCardSaleService`'s shape: validate
  the amount, call `RequestPaymentAsync`, branch on the three outcomes. On `Confirmed`, persist
  the same `Sale`+`Transaction` (`PaymentMethod.MobileMoney`) shape as the other two modules via
  `ISaleRepository`, then increment `Till.MobileMoneyTotal` via `ITillRepository` (fetch-mutate-
  update, the same pattern `CashSaleService` already established).
- **`MAUI-POS-DASH/Components/Pages/MobileMoneySale.razor`** — same session/shift guard pattern as
  the other two payment pages. While the request is in flight, the button is disabled and shows
  "Waiting for customer confirmation…", the same disabled-button pattern `FleetCardSale.razor`
  already uses for its card-tap wait.

## Data flow

`MobileMoneySaleService.ProcessSaleAsync(Guid shiftId, decimal amount, CancellationToken)`:

1. `amount <= 0` → throw `ArgumentException`. Nothing happens, no request goes out.
2. `await _paymentService.RequestPaymentAsync(amount, cancellationToken)`.
3. `Declined` → return `MobileMoneySaleResult(MobileMoneySaleStatus.Declined, null, null,
   result.DetailMessage)`. Nothing persisted.
4. `TimedOut` → return `MobileMoneySaleResult(MobileMoneySaleStatus.TimedOut, null, null,
   "The customer didn't confirm in time.")`. Nothing persisted.
5. `Confirmed` → build `Sale` (single fixed `"Fuel"` line, same as FleetCard/Cash) and
   `Transaction { Method = PaymentMethod.MobileMoney, Status = TransactionStatus.Pending, ... }`,
   persist via `_saleRepository.AddSaleWithTransactionAsync`. Then
   `_tillRepository.GetByShiftIdAsync(shiftId)` (throws `InvalidOperationException` if somehow
   missing — same invariant Cash relies on) → `till.MobileMoneyTotal += amount` →
   `_tillRepository.UpdateAsync(till)`. Return `MobileMoneySaleResult(Confirmed, sale, transaction,
   "Payment confirmed.")`.

`SimulatedMobileMoneyPaymentService.RequestPaymentAsync`: `await Task.Delay(_confirmationDelay,
cancellationToken)`, then roll `_random.NextDouble()`: `< 0.15` → TimedOut, `< 0.35` → Declined,
otherwise → Confirmed.

Because the Transaction is written with `Status = Pending`, it's picked up by the existing
offline sync queue automatically, exactly as with the other two modules.

## Error handling & UI states

`MobileMoneySale.razor`: same no-session/no-open-shift guards as `FleetCardSale.razor` and
`CashSale.razor`. Invalid amount → inline error from the caught `ArgumentException`, no request
sent. While awaiting `ProcessSaleAsync`, the button is disabled and reads "Waiting for customer
confirmation…". `Declined`/`TimedOut` → show the detail message, re-enable the form so the
attendant can retry. `Confirmed` → show the amount and Sale ID, with a "Take Another Payment"
reset, matching the other two modules.

## Testing

Seeded SQLite in-memory `TerminalDbContext`, same shape as `FleetCardSaleServiceTests`:

- `SimulatedMobileMoneyPaymentServiceTests`: constructed with `TimeSpan.Zero` and a `Random`
  subclass forced to specific `NextDouble()` values — one test per outcome boundary (e.g. `0.10` →
  TimedOut, `0.25` → Declined, `0.60` → Confirmed).
- `MobileMoneySaleServiceTests`: a hand-written fake `IMobileMoneyPaymentService` (not the real
  simulator, so tests don't wait through `confirmationDelay` and outcomes are fully scripted) —
  Confirmed persists Sale+Transaction and increments `Till.MobileMoneyTotal`; Declined and
  TimedOut both persist nothing and leave the Till untouched; invalid amount throws
  `ArgumentException` before the fake payment service is ever called (asserted via a call-count
  field, same pattern `FleetCardSaleServiceTests` already uses); missing Till throws
  `InvalidOperationException` on an otherwise-Confirmed outcome.

## Out of scope (explicitly deferred)

- The FleetCard retrofit (feeding `Till.FleetCardTotal`) — separate follow-up task, per the user's
  explicit sequencing instruction.
- Product/pump catalog and itemized fuel lines — single fixed `"Fuel"` line only, matching the
  other two modules' existing scope cut.
- A real mobile money provider integration — `IMobileMoneyPaymentService` is the seam a real
  adapter would implement later.
