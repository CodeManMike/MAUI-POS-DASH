# MAUI-POS-DASH

A forecourt payment terminal, built as a portfolio/showcase project demonstrating .NET MAUI
(Android) and Blazor working together: a MAUI Blazor Hybrid terminal app and a Blazor Web
back-office dashboard, sharing a UI component library and a common domain/persistence layer.

The terminal ships as **two separate UI implementations against the same business logic** — the
primary Blazor Hybrid app, and a plain MAUI XAML+MVVM app (`MAUI-POS-DASH.MAUI-Android`) built to
demonstrate the same flows through the traditional MAUI pattern. See "UI architecture: two
implementations" in `docs/ARCHITECTURE.md` for why.

It's scoped and modeled after a real job: an Android terminal on PAX hardware consolidating
fleet card processing, mobile money, cash handling, shift management, and attendant management
for a fuel forecourt. See `docs/ARCHITECTURE.md` for the full design and `AGENTS.md` for how the
team (human and AI) works in this repo.

## Projects

- **MAUI-POS-DASH** — the terminal app (MAUI Blazor Hybrid, Android only).
- **MAUI-POS-DASH.MAUI-Android** — the same terminal's core flows (login, shifts, all three
  payment methods, attendant management), reimplemented as plain MAUI XAML + MVVM
  (`CommunityToolkit.Mvvm`), Android only. References `Core`/`Core.Persistence` directly — no
  duplicated business logic.
- **MAUI-POS-DASH.Web** / **MAUI-POS-DASH.Web.Client** — the back-office dashboard (Blazor Web
  App, server + WASM).
- **MAUI-POS-DASH.Shared** — Razor UI components used by both hosts.
- **MAUI-POS-DASH.Core** — domain models, DTOs, and interfaces. No EF Core, no ASP.NET — usable
  from anywhere.
- **MAUI-POS-DASH.Core.Persistence** — EF Core: `TerminalDbContext` (SQLite, on-device) and
  `BackofficeDbContext` (Postgres, back office).
- **MAUI-POS-DASH.Core.Tests** — NUnit tests for `Core`.

## Running it

**Web dashboard:**

```bash
dotnet run --project MAUI-POS-DASH.Web
```

Needs a local Postgres reachable at the connection string in
`MAUI-POS-DASH.Web/appsettings.Development.json` (defaults to
`Host=localhost;Database=mauiposdash_dev;Username=postgres;Password=postgres` — a throwaway dev
value, never a real credential). Override it with a user-secret or the
`ConnectionStrings__Backoffice` environment variable rather than editing the committed file.

**MAUI terminal (Android) — either implementation:**

Open the solution in Visual Studio / Rider with the Android workload installed, set either
`MAUI-POS-DASH` (Blazor Hybrid) or `MAUI-POS-DASH.MAUI-Android` (XAML + MVVM) as the startup
project, and run on an emulator or device. Each uses its own local SQLite file under its app's
data directory — no external database needed, and no conflict between the two since they're
separate Android app IDs.

On first run, if no attendants exist yet, a default "Manager" attendant is seeded with PIN
`0000` — a known local/demo credential, not a real one. Sign in with it once to create real
attendants via the Attendants page, then deactivate or change it.

## Tests

```bash
dotnet test MAUI-POS-DASH.Core.Tests
```

## EF Core migrations

A local tool manifest pins `dotnet-ef` to the same version for everyone:

```bash
dotnet tool restore
dotnet ef database update --project MAUI-POS-DASH.Core.Persistence --context TerminalDbContext
dotnet ef database update --project MAUI-POS-DASH.Core.Persistence --context BackofficeDbContext
```

## Status

This is a platform skeleton — see the module ownership table in `docs/ARCHITECTURE.md` for
what's built versus what's an open claim.
