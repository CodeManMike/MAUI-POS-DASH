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
| `Core/Modules/FleetCard/` | unclaimed | `ICardReaderService`, `PaymentMethod` |
| `Core/Modules/MobileMoney/` | unclaimed | `ITransactionSyncService` |
| `Core/Modules/Cash/` | unclaimed | `TillReconciliationService` |
| `Core/Modules/AttendantMgmt/` | unclaimed | `Attendant`, auth |
| Sync endpoint persistence (`Web/Api/TransactionsApi.cs`) | Builder | `BackofficeDbContext` |

Claim a row by editing this table and the module's own `README.md`, in the same commit that
starts the work.

## What's a placeholder right now

- `Login.razor`, `ShiftOpen.razor`, `ShiftClose.razor` in the terminal app are UI-only — they
  don't call `ShiftService` yet (that needs a signed-in attendant).
- `FleetCardSale.razor`, `MobileMoneySale.razor`, `CashSale.razor`, `AttendantManagement.razor`
  are "coming soon" pages pointing at their module's README.
- `POST /api/transactions` returns `501 Not Implemented` — it proves the contract compiles, not
  that it persists anything yet.
- The dashboard's `Dashboard.razor` renders fixed sample data, not a live query.
