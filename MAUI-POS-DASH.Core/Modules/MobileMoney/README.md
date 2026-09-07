# Mobile Money module

**Status:** done. **Owner:** Architect.

See `docs/superpowers/specs/2026-09-07-mobile-money-module-design.md` for the design.

Handles mobile money payment for a fuel sale — the attendant enters an amount,
`IMobileMoneyPaymentService` (currently `SimulatedMobileMoneyPaymentService`, standing in for a
real provider) waits for the customer to confirm on their phone, and on confirmation
`MobileMoneySaleService` persists a `Sale` (one fixed `"Fuel"` line) and a `Transaction` with
`PaymentMethod.MobileMoney` via `ISaleRepository`, then increments `Till.MobileMoneyTotal`.

**Real code lives in:** `Core/MobileMoney/` (`IMobileMoneyPaymentService`,
`SimulatedMobileMoneyPaymentService`, `MobileMoneySaleService`, `MobileMoneySaleResult`).

**Depends on:** `Core/Sales/ISaleRepository.cs`, `Core/Shifts/ITillRepository.cs`. (Earlier
versions of this README named `Core/Sync/ITransactionSyncService.cs` — that interface is the
Terminal→Backoffice batch sync built in PR #3, not a fit for confirming one in-progress payment
with a mobile money provider. This module gets its own confirmation interface instead, shaped
after FleetCard's `IFleetCardAuthorizationService`.)

**Out of scope for now:** a real mobile money provider (the simulator resolves to Confirmed/
Declined/TimedOut after a ~4 second simulated delay), itemized fuel lines (no product/pump
catalog exists yet), and split-tender mobile money payments.
