# Fleet Card module

**Status:** done. **Owner:** Architect.

See `docs/superpowers/specs/2026-09-05-fleet-card-module-design.md` for the design.

Handles fleet card authorization and settlement for a fuel sale — the attendant enters an amount,
the fleet operator's card is tapped via `ICardReaderService`, the terminal authorizes it against
`IFleetCardAuthorizationService` (currently `SimulatedFleetCardAuthorizationService`, standing in
for a real fleet-card backend), and on approval a `Sale` (one fixed `"Fuel"` line for the entered
amount) and a `Transaction` with `PaymentMethod.FleetCard` are persisted via `ISaleRepository`.
The Transaction is picked up by the existing offline sync queue automatically.

**Real code lives in:** `Core/FleetCard/` (`IFleetCardAuthorizationService`,
`SimulatedFleetCardAuthorizationService`, `FleetCardSaleService`, `FleetCardSaleResult`) and
`Core/Sales/ISaleRepository` (shared — Cash and MobileMoney will also depend on this).

**Depends on:** `Core/Devices/ICardReaderService.cs`, `Core/Domain/PaymentMethod.cs`,
`Core/Sales/ISaleRepository.cs`.

**Out of scope for now:** a real fleet-card backend (the simulator declines ~20% of the time to
exercise the decline UI path), itemized fuel lines (no product/pump catalog exists yet), and
split-tender fleet card payments.
