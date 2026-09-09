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
would otherwise have repeated. This app now syncs too, sharing the same `HttpTransactionSyncService`
the Blazor app uses (it now lives in `MAUI-POS-DASH.Core/Sync` so both apps reuse one
implementation instead of duplicating it) — a sync attempt fires after each completed sale and on
every Home appearing.

There's deliberately no `Models` folder alongside `Views`/`ViewModels` here — the Model half of
this app's MVVM is `MAUI-POS-DASH.Core`'s domain model (`Attendant`, `Shift`, `Sale`,
`Transaction`, and each module's result types), shared with the Blazor app rather than redefined
locally. See the comment in `MAUI-POS-DASH.MAUI-Android/GlobalUsings.cs`.

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
| Sync endpoint persistence (`Web/Api/TransactionsApi.cs`) | Builder, Sale-sync fix by Architect (done) | `BackofficeDbContext` |

Claim a row by editing this table and the module's own `README.md`, in the same commit that
starts the work.

## What's a placeholder right now

- All three payment modules (`FleetCardSale.razor`, `CashSale.razor`, `MobileMoneySale.razor`) are
  now working flows (see their specs under `docs/superpowers/specs/`) — all three always record a
  single fixed `"Fuel"` sale line, since there's no product/pump catalog yet.
- `POST /api/transactions` persists idempotent transaction batches, and Sales now travel alongside
  Transactions in the same request and are created in the back office if they don't already exist.
  Shift/Attendant graph synchronization and terminal authentication remain separate follow-up work
  before the sync boundary is production-complete.
- The dashboard's `Dashboard.razor` renders fixed sample data, not a live query.
- Session idle/timeout isn't implemented — a signed-in attendant stays signed in until they
  explicitly sign out (see `docs/superpowers/specs/2026-09-04-attendant-mgmt-design.md` §8).
- A QA pass over the Sale sync fix and all three payment services found and fixed the concrete
  bugs it caught, but flagged a few design gaps left as known, deliberate follow-up rather than
  rushed under deadline pressure:
  - `Sale.ShiftId` has no FK to `Shift`. That's now half-fixed: `FleetCardSaleService`,
    `CashSaleService`, and `MobileMoneySaleService` all check the shift is still `Open` before
    recording a sale against its Till, via the shared
    `MAUI_POS_DASH.Core.Sales.SalePreconditions.GetOpenShiftTillAsync` helper, throwing
    `InvalidOperationException` if it's closed. The database-level FK constraint itself is still
    missing — that's a schema/migration change and remains separate, not-yet-done hardening.
  - Fixed: currency amounts are now validated to have at most 2 decimal places before a payment
    service ever builds a `Sale` — `SalePreconditions.ValidateCurrencyAmount` rejects anything that
    wouldn't round-trip through the backoffice's `numeric(18,2)` column, closing the gap where a
    sub-cent amount could round-trip through the terminal's SQLite `TerminalDbContext` unchanged and
    then get rounded differently once Postgres received it on sync.
  - The three payment services (`FleetCardSaleService`, `CashSaleService`, `MobileMoneySaleService`)
    still duplicate the same build Sale/Transaction → persist → update-Till sequence almost verbatim.
    This is now partially addressed: the shift/till lookup and currency validation are shared via
    `SalePreconditions`, so that class of bug gets fixed once instead of three times going forward.
    But building the Sale/Transaction and updating the till's own total field is still separate per
    service — each increments a different `Till` field (`FleetCardTotal`/`CashTotal`/
    `MobileMoneyTotal`) and has different upstream branching before it — so the construction/persist
    half of the duplication remains.
  - `AttendantService.EnsureNotLastActiveManagerAsync` checks the target attendant's `Role` but not
    whether they're already `IsActive` — an edge case with low real-world impact.
- A Codex review of the MAUI MVVM app's `AttendantManagementViewModel` flagged that `AttendantService`
  has no Manager-role check of its own — every method trusts the caller, and the Manager-only gate
  (`AttendantManagementViewModel.IsAuthorized`) exists only in that one ViewModel's UI state. Nothing
  currently calls `AttendantService`'s CRUD methods except through that gated screen, so this is a
  design gap rather than an exploitable one today, but it means a future caller (a second screen, an
  API surface) would get no protection unless it independently re-implements the same check. Left as
  known follow-up rather than restructuring the service layer under interview deadline pressure.
