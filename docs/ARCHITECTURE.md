# Architecture

This is the living reference for how MAUI-POS-DASH is put together — see
`docs/superpowers/specs/2026-09-03-forecourt-terminal-architecture-design.md` for the
point-in-time design record this was built from.

## Layering

```
MAUI-POS-DASH.Core            domain models, DTOs, interfaces — no EF, no ASP.NET
MAUI-POS-DASH.Core.Persistence   EF Core: TerminalDbContext (SQLite), BackofficeDbContext (Postgres)
MAUI-POS-DASH.Shared           Razor UI components — references Core for DTO types only
MAUI-POS-DASH                  MAUI Blazor Hybrid terminal — references Core + Core.Persistence + Shared
MAUI-POS-DASH.Web              ASP.NET host + sync API — references Core + Core.Persistence + Web.Client
MAUI-POS-DASH.Web.Client       WASM dashboard pages — references Core + Shared only (no EF Core in the browser bundle)
```

## Data flow

1. A sale on the terminal writes to `TerminalDbContext` (SQLite) immediately — never blocked on
   network.
2. `OfflineTransactionQueue` periodically calls `ITransactionSyncService`, POSTing pending
   transactions to `MAUI-POS-DASH.Web`'s `/api/transactions`.
3. The dashboard (`MAUI-POS-DASH.Web.Client`) reads from `BackofficeDbContext` (Postgres) — only
   what's been synced, never talking to a terminal directly.

## Device abstraction

`Core/Devices/*` interfaces are shaped like PAX's real Android SDKs. The current implementations
(`MAUI-POS-DASH/Platforms/Android/Devices/Simulated*`) are simulators — swapping in the real PAX
SDK later means implementing the same interfaces, not rewriting callers.

## Module ownership

| Module | Owner | Depends on |
|---|---|---|
| `Core/Modules/FleetCard/` | Architect (done) | `ICardReaderService`, `PaymentMethod` |
| `Core/Modules/MobileMoney/` | Architect (done) | `ISaleRepository`, `ITillRepository` |
| `Core/Modules/Cash/` | Architect (done) | `TillReconciliationService` |
| `Core/Modules/AttendantMgmt/` | Architect (done) | `Attendant`, auth |
| Sync endpoint persistence (`Web/Api/TransactionsApi.cs`) | Builder (done) | `BackofficeDbContext` |

Claim a row by editing this table and the module's own `README.md`, in the same commit that
starts the work.

## What's a placeholder right now

- All three payment modules (`FleetCardSale.razor`, `CashSale.razor`, `MobileMoneySale.razor`) are
  now working flows (see their specs under `docs/superpowers/specs/`) — all three always record a
  single fixed `"Fuel"` sale line, since there's no product/pump catalog yet.
- `POST /api/transactions` persists idempotent transaction batches whose Sales already exist in
  the back office. Sale/Shift graph synchronization and terminal authentication remain separate
  follow-up work before the sync boundary is production-complete.
- The dashboard's `Dashboard.razor` renders fixed sample data, not a live query.
- Session idle/timeout isn't implemented — a signed-in attendant stays signed in until they
  explicitly sign out (see `docs/superpowers/specs/2026-09-04-attendant-mgmt-design.md` §8).
- `Till.FleetCardTotal` is mapped but nothing updates it yet — `FleetCardSaleService` predates the
  Till lifecycle Cash introduced and doesn't feed it the way `CashSaleService`/`MobileMoneySaleService`
  do for their own totals. This is a known, planned follow-up (retrofit `FleetCardSaleService` to
  match), not an oversight discovered now. Till reconciliation at shift close currently
  undercounts any shift that took fleet card payments.
