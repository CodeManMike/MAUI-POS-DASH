# Forecourt Terminal Showcase — Architecture Design

**Date:** 2026-09-03
**Status:** Approved
**Author:** Architect (Claude), in collaboration with Overmind

## 1. Purpose

Overmind is pursuing a contract role building a new Android terminal application (targeting PAX
payment hardware) for a fuel forecourt environment, consolidating fleet card processing, mobile
money, cash handling, shift management, and attendant management into a single terminal app. The
team builds in an AI-assisted model (Claude + Cursor as core tooling), with a small senior team.

This repository (`MAUI-POS-DASH`) is being turned from the stock `.NET MAUI Blazor Hybrid and Web
App` template into a public showcase that demonstrates:

- A credible architecture for exactly this kind of system, built with **.NET MAUI targeting
  Android** and **Blazor**, sharing UI between a MAUI Hybrid terminal app and a Blazor Web
  back-office dashboard.
- The engineering judgement to scope a PAX device integration sensibly without real hardware
  (interfaces shaped like PAX's real SDKs, backed by simulators).
- Fluency with an AI-assisted, multi-agent development workflow — this repo is itself worked on
  concurrently by Overmind (human), Architect (Claude), and Builder (Codex), and documents that
  workflow as a first-class concern, not an afterthought.

This document is the design of record for that architecture. It intentionally does not spec out
full business logic for every module — see §7 for what is explicitly out of scope for the first
implementation pass.

## 2. Scope decisions

These were decided collaboratively and constrain everything below:

- **Depth over breadth.** The platform layer (device abstraction, shift/till engine, shared UI,
  offline sync) is built with real care. The five business modules (fleet card, mobile money,
  cash, shift management, attendant management) are scaffolded as clearly-bounded stubs, not
  built out — that work is explicitly left for Overmind/Builder/Architect to pick up module by
  module, using this document as the contract.
- **This pass ships structure and contracts, not business logic.** Interfaces, domain models,
  DTOs, DB schema (via migrations), navigation shell, and a couple of real shared components are
  built for real. Service method bodies inside modules are stubs.
- **Android only.** The MAUI app's `TargetFrameworks` is trimmed to Android — the real job is
  Android/PAX only, and carrying iOS/MacCatalyst/Windows targets would be scope no one asked for.
- **No real PAX hardware or SDK access.** Device interfaces are shaped like PAX's actual Android
  SDKs (async/callback patterns, APDU-style card events, ESC/POS-style print documents) so a real
  PAX adapter is a thin swap-in later, not a rewrite. The default implementation is an in-app
  simulator.

## 3. Solution structure

```
MAUI-POS-DASH.slnx
├── MAUI-POS-DASH/                  # MAUI Blazor Hybrid — the terminal app (Android only)
│   ├── Platforms/Android/          # PAX-shaped simulator adapter implementations live here
│   ├── Components/Pages/           # Terminal-only pages (login, shift open/close, sale shell)
│   └── MauiProgram.cs              # DI registration: Core services + Android adapters
│
├── MAUI-POS-DASH.Web/               # ASP.NET Core host — back-office dashboard + sync API
│   ├── Components/Pages/            # Dashboard pages (shift reconciliation, attendant activity)
│   ├── Api/                         # Minimal API endpoints terminals sync to
│   └── Program.cs
│
├── MAUI-POS-DASH.Web.Client/        # WASM interactivity for the dashboard (template default)
│
├── MAUI-POS-DASH.Shared/            # Razor component library — UI ONLY, no business logic
│   └── Components/                  # ShiftSummaryCard, TransactionRow, CurrencyText, StatusBadge
│
└── MAUI-POS-DASH.Core/              # NEW — plain .NET class library, platform-agnostic
    ├── Domain/                      # EF entities: Shift, Till, Transaction, Attendant, Sale...
    ├── Contracts/                   # DTOs used across API/UI boundaries
    ├── Devices/                     # ICardReaderService, IReceiptPrinterService, IBarcodeScannerService
    ├── Shifts/                      # IShiftRepository, ShiftService, reconciliation logic
    ├── Sync/                        # ITransactionSyncService, OfflineTransactionQueue contract
    ├── Persistence/                 # TerminalDbContext (SQLite), BackofficeDbContext (Postgres)
    └── Modules/                     # FleetCard/, MobileMoney/, Cash/, AttendantMgmt/ — stub folders,
                                      # each with its own README describing intended scope
```

**Dependency rule:** `Core` depends on nothing project-specific — no MAUI, no ASP.NET (EF Core is a
portable package, so it's fine there). The MAUI app and Web app both reference `Core` and
`Shared`. `Shared` references nothing but itself. A module folder under `Core/Modules/*` is one
contributor's territory — this is the boundary that lets Overmind, Builder, and Architect work in
parallel without touching the same files.

## 4. Domain model & interfaces

**Domain models** (EF entities in `Core/Domain`): `Attendant` (id, name, PIN hash, role), `Shift`
(id, attendant, opened/closed at, opening float, closing counts, status), `Till` (running totals
per tender type), `Sale` (line items, fuel/product refs, total), `Transaction` (sale + tender
method + status: `Pending`/`Synced`/`Failed`), `PaymentMethod` (enum: `Cash`, `FleetCard`,
`MobileMoney`).

**DTOs** (`Core/Contracts`): one DTO per entity that crosses an API or UI boundary. Entities never
cross a project boundary directly — always mapped to/from a DTO. Mapping is generated by
**Riok.Mapperly** (compile-time source-generated, MIT-licensed) rather than AutoMapper, which
moved to a commercial license in 2024 — worth calling out explicitly as a deliberate, current
engineering decision.

**Platform interfaces** (`Core/Devices`) — the swap point for the real PAX SDK later:

- `ICardReaderService` — `Task<CardReadResult> WaitForCardAsync(CancellationToken)` plus
  tap/insert/remove events, modeled on PAX's async callback style.
- `IReceiptPrinterService` — `Task PrintAsync(ReceiptDocument)`, an ESC/POS-style line-item
  document model.
- `IBarcodeScannerService` — event-based scan stream.

**Application services** (Core, no UI knowledge): `ShiftService` (open/close, float validation),
`TillReconciliationService` (expected vs. counted, variance flagging), `OfflineTransactionQueue`
(SQLite-backed, `Enqueue`/`TryFlush`), `ITransactionSyncService` (HTTP implementation posts queued
transactions to `/api/transactions` when connectivity returns).

**Module ownership map** (lives in `docs/ARCHITECTURE.md`, mirrored here):

| Module | Owner | Depends on |
|---|---|---|
| `FleetCard/` | unclaimed | `ICardReaderService`, `PaymentMethod` |
| `MobileMoney/` | unclaimed | `ITransactionSyncService` |
| `Cash/` | unclaimed | `TillReconciliationService` |
| `AttendantMgmt/` | unclaimed | `Attendant`, auth |

A contributor (Overmind, Architect, or Builder) claims a row before touching that module's folder.

## 5. Data flow

Offline-first, matching a real forecourt's unreliable connectivity:

1. Attendant completes a sale on the terminal → `Sale`/`Transaction` written to
   `TerminalDbContext` (SQLite) immediately, status `Pending`; receipt printed via
   `IReceiptPrinterService`. The sale is never blocked on network.
2. `OfflineTransactionQueue` periodically (and on connectivity-restored) calls
   `ITransactionSyncService.SyncAsync()`, POSTing pending transactions to the Web app's
   `/api/transactions` endpoint. On success, local status flips to `Synced`.
3. The dashboard reads from `BackofficeDbContext` (Postgres) — synced transactions and shift
   records only. It never talks to a terminal directly.

This is a deliberate simplification: a production system would need conflict resolution and
idempotency keys on the sync endpoint. That's documented as a known future concern, not solved in
this pass.

## 6. Error handling

Device and sync failures are modeled as **state, not exceptions bubbling to the UI** —
`CardReadResult`/`SyncResult` carry a status enum (`Success`/`Timeout`/`DeviceUnavailable`/
`NetworkUnavailable`). The terminal shell surfaces a persistent connectivity/device-health
indicator rather than modal error dialogs — a real till has to keep working through a flaky card
reader or a dropped connection, not halt.

## 7. What's in scope for this implementation pass (skeleton)

Built for real:

- Solution restructure: add `MAUI-POS-DASH.Core`; trim the MAUI app to Android-only
  `TargetFrameworks`.
- `Core`: domain models + DTOs (fully defined, XML-documented); all platform interfaces (fully
  defined); `TerminalDbContext` (SQLite) and `BackofficeDbContext` (Postgres) with entities
  mapped and an initial code-first migration generated for each. Service method bodies inside
  modules are `// TODO(...)` stubs — the schema and contracts are real, the logic isn't yet.
- Simulator adapters (`SimulatedPaxCardReader`, etc.): minimal canned-response implementations —
  enough that DI wiring compiles and the terminal shell runs end-to-end on fake data.
- MAUI terminal shell: navigation shell, login placeholder, shift open/close placeholder, module
  tile grid linking to `FleetCard`/`MobileMoney`/`Cash`/`AttendantMgmt` "coming soon" pages.
- Web app: dashboard shell + one stub sync endpoint (`POST /api/transactions`), proving the
  contract compiles across the wire.
- `Shared`: real, working components — `ShiftSummaryCard`, `TransactionRow`, `CurrencyText`,
  `StatusBadge` — the cheapest, most visible proof that MAUI and Web share UI.
- One NUnit test project, `MAUI-POS-DASH.Core.Tests`, demonstrating the seed-then-teardown
  `DbContext` fixture pattern and the Arrange/Act/Assert region convention, with a couple of real
  example tests.
- `README.md`, `docs/ARCHITECTURE.md`, `AGENTS.md` (canonical conventions), `CLAUDE.md` (pointer
  to `AGENTS.md`).

Explicitly out of scope for this pass (left for follow-up, module by module): the actual business
logic inside `FleetCard/`, `MobileMoney/`, `Cash/`, `AttendantMgmt/`; a real PAX SDK adapter;
sync-endpoint idempotency/conflict resolution; full dashboard functionality beyond the shell; bUnit
coverage for `Shared` components.

## 8. Conventions (also documented in `AGENTS.md`)

- **Database access:** EF Core + LINQ only — no Dapper, no raw SQL. Code-first migrations.
- **Mapping:** Riok.Mapperly for entity↔DTO, not AutoMapper (commercial license as of 2024).
- **Testing:** NUnit. Seed the relevant `DbContext` at the start of a suite, real schema and
  relationships visible in the seed data (not mocked), teardown per test to prevent bleed. Every
  test method uses `#region Arrange` / `#region Act` / `#region Assert`.
- **Code organization:** `#region` blocks throughout every C# file (Fields, Constructor,
  Properties, Public Methods, Private Methods, etc.) for navigation. `GlobalUsings.cs` per
  project instead of per-file usings.
- **Documentation:** proper XML docs (`///`) on all public APIs, endpoints, classes, and methods.
  No top-of-file comment headers. Short inline comments only where an interaction is genuinely
  non-obvious.
- **Voice:** comments and docs written in first person / team voice ("we"/"I"), not third-person
  or passive.
- **Naming:** a little personality where it fits, without undercutting the professional read of
  the repo.
- **Contributor naming:** Overmind (human), Architect (Claude), Builder (Codex) — used in docs,
  comments, and the module ownership table. Git commit messages and PR descriptions carry no
  attribution trailer.
- **Secrets:** the Postgres connection string is never committed — local dev uses user-secrets or
  an environment variable, documented in `README.md`.

## 9. Team coordination artifacts

- `docs/ARCHITECTURE.md` — this document's content, kept as a living reference (this spec is the
  point-in-time record; `ARCHITECTURE.md` is what the team actually reads day to day).
- `AGENTS.md` — canonical conventions and the module-claiming protocol. Read by Codex and any
  other agent tooling.
- `CLAUDE.md` — one line pointing at `AGENTS.md`, so there is a single source of truth instead of
  two files drifting apart.
