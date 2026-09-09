# MAUI-POS-DASH

A forecourt payment terminal, built as a portfolio project. It's scoped and modeled after a real
job: an Android terminal on PAX hardware consolidating fleet card processing, mobile money, cash
handling, shift management, and attendant management for a fuel forecourt, plus a back-office
dashboard that reads what's synced from it.

The interesting part isn't the domain — it's the structure. One shared business-logic core
(`MAUI-POS-DASH.Core` / `MAUI-POS-DASH.Core.Persistence`) is fronted by **two complete terminal
UIs**, built using .NET MAUI's two supported ways of shipping a native app: a **MAUI Blazor
Hybrid** app and a **plain MAUI XAML + MVVM** app. Neither is a stripped-down demo of the other —
both implement the same eight screens, call the exact same `Core` services, and now sync to the
same back office. They exist side by side deliberately, to make an architectural choice explicit
and comparable instead of picking one pattern and hoping it reads as the only option available.

## Two ways to build the same terminal on .NET MAUI

| | **MAUI Blazor Hybrid** (`MAUI-POS-DASH`) | **MAUI XAML + MVVM** (`MAUI-POS-DASH.MAUI-Android`) |
|---|---|---|
| UI technology | Razor components in a WebView | Native XAML views, `CommunityToolkit.Mvvm` ViewModels |
| Shares code with | The Web dashboard (`MAUI-POS-DASH.Shared`) | Nothing outside this app — no web/XAML crossover |
| Per-screen ceremony | A page's `@code` block *is* the presentation logic | A dedicated `ViewModel` class per screen, with `[ObservableProperty]`/`[RelayCommand]` |

**Blazor Hybrid's real advantage here** is code reuse: `CurrencyText`, `StatusBadge`,
`TransactionRow` and friends (`MAUI-POS-DASH.Shared`) render identically on-device and in the
Web dashboard's browser tab — one component, two hosts. Iteration is fast (it's HTML/CSS,
`wwwroot/app.css`), and a simple CRUD screen needs no separate ViewModel class — the page's
`@code` block carries its own state. The cost is real too: the app ships a WebView (a genuine
browser engine embedded in the process, with the startup/memory overhead that implies over a
fully native UI tree), and Blazor's binding model is push-based re-rendering, not the
two-way `INotifyPropertyChanged` chain MVVM frameworks are built around.

**Plain MAUI XAML + MVVM's real advantage** is that every control is a real native Android view —
there's no WebView in the picture, so it's the framework with the higher performance and
platform-feel ceiling, and the View/ViewModel separation is enforced by the framework's own
compiled bindings, not just a house convention. The cost shows up in ceremony: every screen needs
its own `ViewModel`, and platform quirks become the app's own problem to solve rather than the
framework's — this repo hit that directly. Android's edge-to-edge enforcement started drawing
content behind the status bar and needed an explicit `MainActivity` fix
(`WindowCompat.SetDecorFitsSystemWindows`), and there's no CSS ecosystem to lean on for styling —
turning the default template's flat, dated look into something that reads as a considered, modern
Android app (`Resources/Styles/Colors.xaml` / `Styles.xaml`) took deliberate design work, not a
stylesheet swap.

Both are honest, complete implementations, not a primary app and a proof-of-concept — see
[User journeys](#user-journeys) below for what each one actually does, screen by screen.

## Projects

- **`MAUI-POS-DASH`** — the Blazor Hybrid terminal app (Android).
- **`MAUI-POS-DASH.MAUI-Android`** — the same terminal's flows (login, shifts, all three payment
  methods, attendant management), built as plain MAUI XAML + MVVM (Android). References `Core` /
  `Core.Persistence` directly — no duplicated business logic, and (as of the shared sync client
  below) no duplicated sync logic either.
- **`MAUI-POS-DASH.Web`** / **`MAUI-POS-DASH.Web.Client`** — the back-office dashboard (Blazor Web
  App: `Web` is the ASP.NET host serving both interactive-server and interactive-WebAssembly
  render modes; `Web.Client` is the WASM half it serves — one process, not two).
- **`MAUI-POS-DASH.Shared`** — Razor UI components used by both the Blazor Hybrid terminal and the
  Web dashboard.
- **`MAUI-POS-DASH.Core`** — domain models, DTOs, and interfaces. No EF Core, no ASP.NET, no
  MAUI — usable from anywhere, which is exactly why both terminal apps and the Web host all
  reference it directly instead of going through a network boundary.
- **`MAUI-POS-DASH.Core.Persistence`** — EF Core: `TerminalDbContext` (SQLite, on-device) and
  `BackofficeDbContext` (Postgres, back office).
- **`MAUI-POS-DASH.Core.Tests`** / **`MAUI-POS-DASH.Web.Tests`** — NUnit tests for `Core` and the
  sync API, against real seeded `DbContext` fixtures rather than mocks.

## Architecture

### Layering

```
MAUI-POS-DASH.Core            domain models, DTOs, interfaces — no EF, no ASP.NET, no MAUI
MAUI-POS-DASH.Core.Persistence   EF Core: TerminalDbContext (SQLite), BackofficeDbContext (Postgres)
MAUI-POS-DASH.Shared           Razor UI components — references Core for DTO types only
MAUI-POS-DASH                  MAUI Blazor Hybrid terminal — references Core + Core.Persistence + Shared
MAUI-POS-DASH.MAUI-Android     MAUI XAML + MVVM terminal — references Core + Core.Persistence directly
MAUI-POS-DASH.Web              ASP.NET host + sync API — references Core + Core.Persistence + Web.Client
MAUI-POS-DASH.Web.Client       WASM dashboard pages — references Core + Shared only (no EF Core in the browser bundle)
```

`Core` never references `Core.Persistence`, MAUI, or ASP.NET — everything above it depends inward,
never sideways or down.

### Data flow

1. A sale on either terminal writes to its own on-device `TerminalDbContext` (SQLite) immediately —
   never blocked on network.
2. `OfflineTransactionQueue` attempts to flush pending transactions via `ITransactionSyncService` —
   `HttpTransactionSyncService` (shared by both terminal apps, living in `MAUI-POS-DASH.Core/Sync`
   so neither one duplicates it) POSTs them to `MAUI-POS-DASH.Web`'s `/api/transactions`. The MVVM
   app triggers a flush attempt after every completed sale and every time its Home screen appears;
   the Blazor app's queue is wired the same way.
3. `TransactionIngestionService` persists the batch idempotently, creating any `Sale` rows the back
   office doesn't already have, and rejecting anything that can't round-trip through Postgres's
   `numeric(18,2)` columns exactly.
4. The dashboard (`MAUI-POS-DASH.Web.Client`) reads from `BackofficeDbContext` — only what's been
   synced, never talking to a terminal directly.

### Session-guard base classes

Both terminal apps centralize the same two checks every authenticated screen needs — "is anyone
signed in" and (for four of the eight screens) "does this attendant have an open shift" — instead
of repeating the check per screen:

- **Blazor Hybrid:** each page's `OnInitializedAsync` calls `AttendantService.GetActiveSessionAsync()`
  directly; the shared `TerminalLayout.razor` (the `DefaultLayout` for every route) renders the
  signed-in attendant, a Home link, and Sign out once, for every page, rather than each page
  repeating that chrome.
- **MVVM:** `ViewModels/AuthenticatedViewModelBase.cs` and `ViewModels/ShiftAwareViewModelBase.cs`
  hold the equivalent checks; `Views/StatusHeaderView.xaml` is the matching shared chrome, placed
  in its own row above each page's scrollable content so it renders as a real top app bar rather
  than an inset row of text. Because MAUI Shell keeps each screen's page and ViewModel alive across
  visits instead of recreating them, `AuthenticatedViewModelBase.ResetVisitState()` exists
  specifically to clear one attendant's leftover input before the next attendant lands on the same
  cached screen — a problem Blazor's per-navigation component lifecycle doesn't have.

### Device abstraction

`Core/Devices/*` interfaces are shaped like PAX's real Android SDKs. The current implementations
(`Platforms/Android/Devices/Simulated*` in the Blazor Hybrid app) are simulators — swapping in the
real PAX SDK later means implementing the same interfaces, not rewriting callers.

## User journeys

### Back of house (`MAUI-POS-DASH.Web.Client`)

The manager-facing dashboard is the thinnest part of this repo today — it renders one hardcoded
sample shift and transaction list, not a live query. The sync pipe that would feed it real data now
works end-to-end (see Data flow above); wiring the dashboard to actually query `BackofficeDbContext`
is the natural next slice, not yet built.

1. Open the dashboard at `/` — `DashboardLayout` renders a header banner; there's no login on this
   host yet.
2. `Dashboard.razor` renders "Shift Reconciliation": one `ShiftSummaryCard` and a list of
   `TransactionRow`s, built from `_sampleShift` / `_sampleTransactions` in the page itself.

### Terminal — Blazor Hybrid

1. **Home** (`/`) renders six module tiles — Fleet Card, Mobile Money, Cash, Attendants, Open
   Shift, Close Shift — after confirming a session exists (redirects to `/login` otherwise, the
   same guard every other page uses).
2. **Login** (`/login`) shows a tile per active attendant (seeded on first run: a "Manager"
   account, PIN `0000`, a known throwaway demo credential); picking one shows a PIN pad. A wrong
   PIN or a deactivated attendant shows an inline message; success returns to Home.
3. **Open Shift** (`/shift/open`) takes an opening float and calls `ShiftService.OpenShiftAsync`
   (rejects a negative float), then returns to Home.
4. **Fleet Card / Mobile Money / Cash** (`/fleet-card`, `/mobile-money`, `/cash`) each show a
   no-shift message if there's nothing open, otherwise an amount-entry form. Submitting calls the
   matching `*SaleService` — a simulated card wait, a delayed mobile-money confirmation, or an
   instant cash tender — and shows an approved/declined result with the sale ID and amount, or lets
   the attendant take another payment.
5. **Close Shift** (`/shift/close`) takes a cash count, shows expected-vs-counted variance and a
   tolerance verdict via `ShiftService.PreviewCloseAsync`, then either confirms the close or lets
   the attendant recount.
6. **Attendants** (`/attendants`, Manager-only) lists active attendants with inline Edit/Deactivate,
   plus a create/edit form; a Manager who demotes themselves loses the authorized view immediately,
   not on next visit.

Every screen shares one `TerminalLayout` header with the signed-in attendant's name, a Home link,
and Sign out.

### Terminal — MAUI XAML + MVVM

Structurally identical to the Blazor journey above — same eight screens, same `Core` services,
same absolute Shell-route navigation between top-level screens — expressed through XAML views bound
to `CommunityToolkit.Mvvm` ViewModels instead of Razor `@code` blocks:

1. **Login** — `LoginViewModel` loads the same attendant tiles from the same `AttendantService`;
   PIN entry calls the same `TryLoginAsync`, styled as a tonal tile grid rather than a table.
2. **Home** — six tonal launcher tiles bound to `[RelayCommand]`s doing
   `Shell.Current.GoToAsync("//RouteName")`; every screen carries a shared `StatusHeaderView` top
   app bar (Home / attendant name / Sign out) instead of Blazor's layout-level header.
3. **Open Shift / Close Shift / the three payment screens** — same validation, same service calls,
   same result states, presented as rounded Material-style cards with bordered input fields instead
   of HTML forms.
4. **Attendants** — the same Manager-only CRUD, with attendant rows as compact list cards
   (Edit/Deactivate) instead of a table.
5. **What this app does differently:** every screen's ViewModel resets its own per-visit state
   (a typed-but-unsubmitted amount, a completed sale result) on every appearing, because Shell
   caches the page instance across visits — see `ResetVisitState()` in the base classes above. And
   it deliberately has no local `Models` folder: the Model half of its MVVM is `Core`'s domain
   model, shared with the Blazor app rather than redefined locally (see the comment in
   `GlobalUsings.cs`).

## Running it

### 1. Back office: Postgres + the sync API + the dashboard

Start Postgres (a local install, or via Docker):

```bash
docker run --name mauiposdash-pg -e POSTGRES_DB=mauiposdash_dev -e POSTGRES_USER=postgres -e POSTGRES_PASSWORD=postgres -p 5432:5432 -d postgres:16
```

Apply the backoffice schema (`dotnet tool restore` once first, to pin `dotnet-ef` to the version
this repo uses):

```bash
dotnet tool restore
dotnet ef database update --project MAUI-POS-DASH.Core.Persistence --context BackofficeDbContext
```

Run the Web host — this single process serves both the sync API (`POST /api/transactions`) and the
`Web.Client` WASM dashboard:

```bash
dotnet run --project MAUI-POS-DASH.Web
```

It listens on `https://localhost:7135` and `http://localhost:5075` by default (see
`MAUI-POS-DASH.Web/Properties/launchSettings.json`). It needs Postgres reachable at the connection
string in `MAUI-POS-DASH.Web/appsettings.Development.json` (defaults to
`Host=localhost;Database=mauiposdash_dev;Username=postgres;Password=postgres` — a throwaway dev
value, never a real credential) — override it with a user-secret or the
`ConnectionStrings__Backoffice` environment variable rather than editing the committed file.

### 2. A terminal (Android) — either implementation

Apply the terminal schema too, so the design-time migration exists (the app also applies pending
migrations itself on launch, against its real runtime database):

```bash
dotnet ef database update --project MAUI-POS-DASH.Core.Persistence --context TerminalDbContext
```

Open the solution in Visual Studio / Rider with the Android workload installed, set either
`MAUI-POS-DASH` (Blazor Hybrid) or `MAUI-POS-DASH.MAUI-Android` (XAML + MVVM) as the startup
project, and run on an emulator or device. Each uses its own local SQLite file under its app's data
directory — no conflict between the two, since they're separate Android app IDs.

On first run, if no attendants exist yet, a default "Manager" attendant is seeded with PIN `0000`.
Sign in with it once to create real attendants via the Attendants screen, then deactivate or change
it.

**To see a terminal sale actually reach the back office**, the terminal needs to reach the Web
host's `https://localhost:7135` from the device/emulator:

- **Physical device over `adb`:** `adb reverse tcp:7135 tcp:7135` (repeat with your device's serial
  via `-s` if more than one is attached).
- **Android emulator:** use `https://10.0.2.2:7135/` instead — the emulator's alias for the host's
  loopback — or `adb reverse` works there too.

Both apps register the HTTP client with a **Debug-only** permissive TLS handler
(`ConfigurePrimaryHttpMessageHandler` in each `ServiceRegistration.cs`), since the ASP.NET Core dev
HTTPS certificate is self-signed and Android doesn't trust it by default — without it, every sync
attempt would silently look identical to "offline" even with the backend fully reachable. Release
builds get normal certificate validation.

### Tests

```bash
dotnet test MAUI-POS-DASH.Core.Tests
dotnet test MAUI-POS-DASH.Web.Tests
```

## What's a placeholder right now

- All three payment modules always record a single fixed `"Fuel"` sale line, since there's no
  product/pump catalog yet.
- Shift/Attendant graph synchronization and terminal authentication remain separate follow-up work
  before the sync boundary is production-complete — `POST /api/transactions` has no caller
  authentication today.
- The dashboard's `Dashboard.razor` renders fixed sample data, not a live query against
  `BackofficeDbContext` — see [Back of house](#back-of-house-maui-pos-dashwebclient) above.
- Session idle/timeout isn't implemented — a signed-in attendant stays signed in until they
  explicitly sign out.
- `Sale.ShiftId` has no database-level foreign key to `Shift` — the *business-rule* check (a
  closed shift can't record a new sale) is enforced in code via
  `MAUI_POS_DASH.Core.Sales.SalePreconditions.GetOpenShiftTillAsync`, shared by all three payment
  services, but the schema-level constraint itself is still missing.
- The three payment services (`FleetCardSaleService`, `CashSaleService`, `MobileMoneySaleService`)
  share their shift/till lookup and currency validation via `SalePreconditions`, but each still
  builds its own `Sale`/`Transaction` and updates a different `Till` field — that half of the
  duplication is unresolved.
- `AttendantService` has no Manager-role check of its own — every method trusts its caller, and the
  Manager-only gate exists only in each terminal's own UI state. Nothing currently calls it except
  through that gated screen, so this is a design gap rather than an exploitable one today, but a
  future caller (a second screen, an API surface) would get no protection unless it independently
  re-implements the same check.
- `AttendantService.EnsureNotLastActiveManagerAsync` checks the target attendant's `Role` but not
  whether they're already `IsActive` — an edge case with low real-world impact.
