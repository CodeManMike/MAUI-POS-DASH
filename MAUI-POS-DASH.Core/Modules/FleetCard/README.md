# Fleet Card module

**Status:** in progress. **Owner:** Architect.

See `docs/superpowers/specs/2026-09-05-fleet-card-module-design.md` for the design.

Handles fleet card authorization and settlement for a fuel sale — the fleet operator's card is
tapped/inserted via `ICardReaderService`, the terminal authorizes against the fleet card
backend, and a `Transaction` with `PaymentMethod.FleetCard` is created.

**Depends on:** `Core/Devices/ICardReaderService.cs`, `Core/Domain/PaymentMethod.cs`.

**To claim this module:** add your name to the Owner line above, update the row in
`docs/ARCHITECTURE.md`, and open a PR/branch scoped to this folder plus the
`MAUI-POS-DASH/Components/Pages/FleetCardSale.razor` placeholder page.
