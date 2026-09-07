# Cash Module Design

**Owner:** Architect. **Status:** approved, not yet implemented.

## Goal

Replace the `CashSale.razor` "coming soon" placeholder with a working cash-tender payment flow,
and close the loop on an existing TODO in `ShiftService.CloseShiftAsync`:

```
// TODO(Builder/Overmind): once the Cash module lands, run TillReconciliationService here
// and surface the variance before allowing close to complete.
```

`TillReconciliationService` and `Till` have existed since the skeleton phase but nothing has ever
created a `Till` row or called `Reconcile`. This module fixes both.

## Why now

Cash and MobileMoney are the two remaining unclaimed payment modules. Cash has no network
dependency (unlike MobileMoney) and directly satisfies a TODO already sitting in the codebase, so
it's the natural next module. It also reuses `Core/Sales/ISaleRepository`, built for FleetCard,
confirming that abstraction actually holds up across a second, independently-designed module.

## Architecture

- **`Core/Shifts/ITillRepository`** + **`Core.Persistence/Repositories/EfTillRepository`** — new.
  `CreateAsync(Till)`, `GetByShiftIdAsync(Guid)`, `UpdateAsync(Till)`. Lives in `Core/Shifts/`,
  not under the Cash module folder, because `Till` and its lifecycle belong to the Shift domain —
  Cash only ever updates one field (`CashTotal`) on it, the same way FleetCard and MobileMoney
  will later update `FleetCardTotal`/`MobileMoneyTotal`.
- **`Core/Shifts/ShiftService`** (modified) — `OpenShiftAsync` now also creates a zero-total
  `Till` for the new shift, making "every open shift has exactly one Till" a real invariant that
  `TillReconciliationService.Reconcile` (which takes a non-nullable `Till`) can rely on. A new
  `PreviewCloseAsync(Shift, decimal cashCounted)` fetches that Till and returns a
  `ReconciliationResult` without writing anything. `CloseShiftAsync` itself is unchanged — it
  still finalizes the close exactly as before.
- **`Core/Cash/CashSaleService`** — no card wait, no authorization step (there's nothing to
  authorize with cash). Validates the amounts, persists the same `Sale`+`Transaction` shape as
  FleetCard via `ISaleRepository`, then increments `Till.CashTotal`.
- **`MAUI-POS-DASH/Components/Pages/CashSale.razor`** — amount owed + cash tendered inputs, shows
  change due. Same session/shift guard pattern as the other payment pages.
- **`MAUI-POS-DASH/Components/Pages/ShiftClose.razor`** (modified) — becomes two-step: enter cash
  counted → "Review" shows expected/counted/variance → "Confirm Close" finalizes, or "Recount"
  returns to step one. This directly satisfies the TODO's "surface the variance before allowing
  close to complete."

## Data flow

**`ShiftService.OpenShiftAsync(attendantId, openingFloat)`** — signature and return type
unchanged. After `_shiftRepository.OpenAsync(shift)` succeeds, builds
`Till { Id = Guid.NewGuid(), ShiftId = shift.Id, CashTotal = 0, FleetCardTotal = 0,
MobileMoneyTotal = 0 }` and calls `_tillRepository.CreateAsync(till)`. This is two separate
`SaveChangesAsync` calls, matching how every repository in this codebase already owns its own
save rather than introducing a new cross-repository transaction pattern — a shift very briefly
existing without its Till on a local SQLite write is a negligible risk, not worth a new
unit-of-work abstraction.

**`CashSaleService.ProcessSaleAsync(Guid shiftId, decimal amountOwed, decimal amountTendered,
CancellationToken)`**:
1. `amountOwed <= 0` → throw `ArgumentException`. `amountTendered < amountOwed` → throw
   `ArgumentException`. Nothing persisted either way.
2. Build `Sale { Id, ShiftId = shiftId, OccurredAt = UtcNow, Lines = [SaleLine { Description =
   "Fuel", UnitPrice = amountOwed, Quantity = 1 }] }` and `Transaction { Id, SaleId = sale.Id,
   Method = PaymentMethod.Cash, Amount = amountOwed, Status = TransactionStatus.Pending, CreatedAt
   = UtcNow }`; persist via `_saleRepository.AddSaleWithTransactionAsync`.
3. `_tillRepository.GetByShiftIdAsync(shiftId)` — since Till is now guaranteed to exist by the
   eager-creation invariant, a `null` result here means that invariant was somehow violated, so
   this throws `InvalidOperationException` rather than silently creating a Till on the spot.
4. `till.CashTotal += amountOwed`; `_tillRepository.UpdateAsync(till)`.
5. Return `CashSaleResult(sale, transaction, amountTendered - amountOwed)`.

`CashSaleResult` carries no status enum — unlike `FleetCardSaleResult`, there's no
decline/failure branch here, only a thrown exception for bad input, so a plain record matches the
actual branching instead of forcing a uniform shape across modules that don't behave the same way.

**`ShiftService.PreviewCloseAsync(Shift shift, decimal cashCounted, CancellationToken)`** — fetches
the shift's Till (same "must exist" invariant; throws `InvalidOperationException` otherwise) and
returns `TillReconciliationService.Reconcile(till, cashCounted)`. No writes. `ShiftClose.razor`
holds the result and the entered `cashCounted` in page state; "Confirm Close" then calls the
existing `CloseShiftAsync(shift, cashCounted)`, completely unchanged.

Because the Transaction is written with `Status = Pending`, it's picked up by the existing offline
sync queue automatically, exactly as with FleetCard — no new sync wiring needed.

## Error handling & UI states

**`CashSale.razor`**: same no-session (redirect `/login`) and no-open-shift guards as the other
payment pages. Invalid amount owed or tendered-less-than-owed → inline error from the caught
`ArgumentException`, no card-wait-style intermediate state since there's no hardware involved.
Success → shows the change due and a "Take Another Payment" reset, matching
`FleetCardSale.razor`'s pattern.

**`ShiftClose.razor`**: unchanged no-open-shift guard. The "Review" step has no failure mode of
its own beyond that guard — cash counted can be any non-negative number, including 0, and always
produces a `ReconciliationResult` (out-of-tolerance is a displayed fact, not an error). "Confirm
Close" can still fail exactly as `CloseShiftAsync` already could before this change — no new error
path is introduced there, since that method itself doesn't change.

## Testing

Seeded SQLite in-memory `TerminalDbContext`, same shape as the existing suites:

- `EfTillRepositoryTests`: create, fetch-by-shift, and update round-trip correctly.
- `ShiftServiceTests` (extending the existing file, which currently constructs `ShiftService`
  with only `EfShiftRepository`): `OpenShiftAsync` also creates a matching zero-total `Till` for
  the new shift; `PreviewCloseAsync` returns the correct expected/counted/variance/tolerance
  without mutating the shift or the Till; `PreviewCloseAsync` throws if the shift's Till is
  somehow missing.
- `CashSaleServiceTests` (fakes not needed here — no external collaborators like a card reader to
  fake; a real `EfSaleRepository`/`EfTillRepository` against the seeded context is enough):
  approved sale persists Sale+Transaction and increments `Till.CashTotal` by the right amount;
  `Change` computes correctly; a non-positive `amountOwed` and a `amountTendered < amountOwed`
  both throw `ArgumentException` with nothing persisted and the Till left untouched.

## Out of scope (explicitly deferred)

- Product/pump catalog and itemized fuel lines — single fixed `"Fuel"` line only, matching
  FleetCard's existing scope cut.
- `FleetCardTotal`/`MobileMoneyTotal` on `Till` — those are for their own modules to update later,
  following the same pattern `CashTotal` establishes here.
- Any change to `CloseShiftAsync`'s own behavior or signature — this module only adds a
  read-only preview step in front of it.
