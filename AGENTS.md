# AGENTS.md

Conventions for anyone — human or AI — working in this repository. This is the canonical file;
`CLAUDE.md` just points here.

## Who's who

- **Overmind** — the human developer.
- **Architect** — Claude, working in this repo.
- **Builder** — Codex, working in this repo.

Use these names in docs, comments, and the module ownership table in `docs/ARCHITECTURE.md`.

## Before touching a module

Check the ownership table in `docs/ARCHITECTURE.md` and the module's own `Core/Modules/*/README.md`.
Claim a row before writing code in that folder — edit both files in the same commit that starts
the work, so two contributors don't pick the same module at the same time.

## Code conventions

- **Database access:** EF Core + LINQ only. No Dapper, no raw SQL. Code-first migrations via the
  local `dotnet-ef` tool (`dotnet tool restore` first).
- **Mapping:** Riok.Mapperly for entity↔DTO (`Core/Contracts/EntityMapper.cs`). Not AutoMapper —
  it moved to a commercial license in 2024.
- **Testing:** NUnit. Seed the relevant `DbContext` at the top of a `[SetUp]` (real schema, real
  relationships — not mocks), dispose it in `[TearDown]`. Every `[Test]` method uses
  `#region Arrange` / `#region Act` / `#region Assert`.
- **Code organization:** `#region` blocks in every C# file (Fields, Constructor, Properties,
  Public Methods, Private Methods, Events, etc. — whichever apply). `GlobalUsings.cs` per project
  instead of repeating common `using`s per file.
- **Documentation:** XML docs (`///`) on all public types and members. No top-of-file comment
  headers. Short inline comments only where an interaction is genuinely non-obvious.
- **Voice:** comments and docs in first person / team voice ("we"/"I"), not third-person or
  passive.
- **Naming:** a little personality where it fits (seed/sample data is a reasonable place for
  this) — without undercutting the professional read of the repo.
- **Commits:** no attribution trailers.
- **Secrets:** never commit a real connection string, API key, or credential. Local dev defaults
  in `appsettings.Development.json` are throwaway values only — real environments use
  user-secrets or environment variables.

## Layering rule

`Core` must never reference `Core.Persistence`, MAUI, or ASP.NET. `Core.Persistence` must never
be referenced by `MAUI-POS-DASH.Web.Client` (it would pull EF Core/Npgsql into the browser
bundle). If a change seems to require breaking either rule, stop and raise it rather than
routing around it.
