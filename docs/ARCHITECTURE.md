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

## UI architecture: two implementations, one business logic layer

This repo deliberately ships **two** terminal UIs against the same `Core`/`Core.Persistence`
business logic, to make an architectural choice explicit rather than assumed.

**`MAUI-POS-DASH` (primary) — MAUI Blazor Hybrid.** Pages are Razor components
(`Components/Pages/*.razor`), the same technology used by the Web dashboard
(`MAUI-POS-DASH.Web.Client`) via the shared `MAUI-POS-DASH.Shared` component library — one UI
codebase runs on-device and in the browser. Blazor doesn't use XAML or MVVM; its equivalent
discipline is enforced here as a hard project rule instead, held since the very first design
session (see `AGENTS.md`): **service-first, dumb UI**. A page's `@code` block only checks session
state, binds inputs, calls exactly one `Core` service method per action, and turns the result or a
caught exception into a displayed message or a navigation — see any page under
`Components/Pages/` for the pattern (e.g. `FleetCardSale.razor`, `CashSale.razor`,
`AttendantManagement.razor`). Every business rule, validation, calculation, and persistence
decision lives in a `Core` service (`FleetCardSaleService`, `CashSaleService`, `AttendantService`,
etc.), never in a page. This was verified by inspection across every page in the app, not assumed.

**`MAUI-POS-DASH.MAUI-Android` (secondary) — plain MAUI, XAML + MVVM.** Android-only, built to
demonstrate the same business logic through the traditional MAUI UI pattern: XAML views bound to
`CommunityToolkit.Mvvm` ViewModels (`[ObservableProperty]`/`[RelayCommand]`, source-generated, no
hand-written `INotifyPropertyChanged`). It references `MAUI-POS-DASH.Core` and
`MAUI-POS-DASH.Core.Persistence` directly and duplicates zero business logic — every screen calls
the exact same service classes the Blazor app calls. Code-behind (`*.xaml.cs`) is limited to
constructor injection setting `BindingContext` and one `OnAppearing` passthrough to the
ViewModel; nothing else is allowed there. See `ViewModels/AuthenticatedViewModelBase.cs` and
`ViewModels/ShiftAwareViewModelBase.cs` for the shared session/shift-guard logic every screen
would otherwise have repeated. This app intentionally excludes the offline sync queue — it isn't
what's being demonstrated, and skipping it removes a dependency on a running backend.

Two working implementations of the same terminal, sharing one tested business-logic layer,
answers "how do you structure your apps" more concretely than either app could alone: it shows
the same domain logic is genuinely UI-framework-agnostic, and shows deliberate reasoning about
which UI pattern fits which framework rather than defaulting to one without considering the
other.

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
