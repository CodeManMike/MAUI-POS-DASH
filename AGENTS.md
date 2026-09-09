# AGENTS.md

Conventions for anyone — human or AI — working in this repository. This is the canonical file;
`CLAUDE.md` just points here.

## Who's who

- **Overmind** — the human developer.
- **Architect** — Claude, working in this repo.
- **Builder** — Codex, working in this repo.

Use these names in docs and comments.

## Code conventions

- **Database access:** EF Core + LINQ only. No Dapper, no raw SQL. Code-first migrations via the
  local `dotnet-ef` tool (`dotnet tool restore` first).
- **Mapping:** Riok.Mapperly for entity↔DTO (`Core/Contracts/EntityMapper.cs`). Not AutoMapper —
  it moved to a commercial license in 2024.
- **UI boundaries:** Prefer service-layer orchestration for both Web and MAUI. Keep pages and
  components deliberately dumb: they present state, bind input, navigate, and call services;
  business rules, validation, persistence, and external integrations belong behind services.
- **Testing:** NUnit. Seed the relevant `DbContext` at the top of a `[SetUp]` (real schema, real
  relationships — not mocks), dispose it in `[TearDown]`. Every `[Test]` method uses
  `#region Arrange` / `#region Act` / `#region Assert`.
- **Code organization:** `#region` blocks in every C# file (Fields, Constructor, Properties,
  Public Methods, Private Methods, Events, etc. — whichever apply). `GlobalUsings.cs` per project
  instead of repeating common `using`s per file.
- **Control flow:** prefer a `switch` statement/expression over an `if`/`else if` chain wherever
  the branches dispatch on a single value (an enum, a status, a type pattern) — it reads as one
  decision instead of a sequence of separate checks, and the compiler can flag a missing case.
  Keep plain `if` for simple guard clauses and boolean conditions where a switch would be forced.
- **Never nest where able:** prefer early returns and guard clauses over wrapping the rest of a
  method in `else`. If an `if`/`else` splits a method into two large branches that both eventually
  do the same trailing work, restructure so each branch handles its own case and returns, rather
  than falling through to shared code at the bottom of a nested block.
- **DbContext scope:** when a method or class needs the same `DbContext` more than once, resolve
  it a single time (constructor injection into a field is the default pattern already used
  throughout `Core.Persistence`'s repositories) and reuse that one reference for every query in
  that scope — never re-resolve it from DI per call.
- **Documentation:** XML docs (`///`) on all public types and members. No top-of-file comment
  headers. Short inline comments only where an interaction is genuinely non-obvious.
- **Voice:** comments and docs in first person / team voice ("we"/"I"), not third-person or
  passive.
- **Naming:** a little personality where it fits (seed/sample data is a reasonable place for
  this) — without undercutting the professional read of the repo.
- **Secrets:** never commit a real connection string, API key, or credential. Local dev defaults
  in `appsettings.Development.json` are throwaway values only — real environments use
  user-secrets or environment variables.

## Layering rule

`Core` must never reference `Core.Persistence`, MAUI, or ASP.NET. `Core.Persistence` must never
be referenced by `MAUI-POS-DASH.Web.Client` (it would pull EF Core/Npgsql into the browser
bundle). If a change seems to require breaking either rule, stop and raise it rather than
routing around it.

## Two framework gotchas found the hard way (both in `MAUI-POS-DASH.Web`)

- **Never return a bare status code from a Minimal API endpoint.** `Results.StatusCode(...)` /
  `TypedResults.StatusCode(...)` produces an empty-bodied 4xx/5xx response, which
  `UseStatusCodePagesWithReExecute("/not-found", ...)` (`Program.cs`) intercepts and re-executes
  against `/not-found` — replaying the original request, POST body and all. That re-execution
  hits a Razor Component endpoint, which tries to read the body as a form post and throws on a
  JSON body. Always return something with an actual response body for error statuses —
  `TypedResults.Problem(statusCode: ..., detail: ...)` is the standard choice.
- **A component passed `@rendermode` can only take JSON-serializable parameters.** Blazor
  serializes every parameter on such a component for the server↔client interactive-resume
  handoff. Don't pass a `System.Reflection.Assembly` (or anything else non-serializable) as a
  `[Parameter]` into one — hardcode it as a literal expression inside the component instead (see
  `MAUI-POS-DASH.Web/Components/AppRoutes.razor`).
