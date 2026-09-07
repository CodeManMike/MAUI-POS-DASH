# Mobile Money module

**Status:** in progress. **Owner:** Architect.

See `docs/superpowers/specs/2026-09-07-mobile-money-module-design.md` for the design.

Handles mobile money payment for a fuel sale — a QR code or USSD prompt is shown, the customer
confirms on their phone, and the terminal waits for that confirmation before creating a
`Transaction` with `PaymentMethod.MobileMoney`.

**Depends on:** `Core/Sales/ISaleRepository.cs`, `Core/Shifts/ITillRepository.cs`. (Earlier
versions of this README named `Core/Sync/ITransactionSyncService.cs` — that interface is the
Terminal→Backoffice batch sync built in PR #3, not a fit for confirming one in-progress payment
with a mobile money provider. This module gets its own confirmation interface instead, shaped
after FleetCard's `IFleetCardAuthorizationService`.)
