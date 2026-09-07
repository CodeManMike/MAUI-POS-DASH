# Cash module

**Status:** done. **Owner:** Architect.

See `docs/superpowers/specs/2026-09-07-cash-module-design.md` for the design.

Handles cash tender and change calculation for a fuel sale. The attendant enters the amount owed
and cash tendered; `CashSaleService` validates the tender covers the amount, persists a `Sale`
(one fixed `"Fuel"` line) and a `Transaction` with `PaymentMethod.Cash` via `ISaleRepository`, and
increments `Till.CashTotal` for the shift. This also closes out a longstanding TODO in
`ShiftService`: every shift now gets a `Till` created when it opens, and closing a shift is now a
two-step flow — review the till-reconciliation variance, then confirm — instead of closing blind.

**Real code lives in:** `Core/Cash/` (`CashSaleService`, `CashSaleResult`) and
`Core/Shifts/ITillRepository` (shared with `ShiftService`'s new Till lifecycle).

**Depends on:** `Core/Shifts/TillReconciliationService.cs`, `Core/Shifts/ITillRepository.cs`,
`Core/Sales/ISaleRepository.cs`.

`FleetCardSaleService` and `MobileMoneySaleService` were retrofitted/built to feed
`Till.FleetCardTotal`/`MobileMoneyTotal` the same way, so till reconciliation at shift close now
reflects all three payment methods.
