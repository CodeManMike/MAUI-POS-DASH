# Attendant Management Module — Design

**Date:** 2026-09-04
**Status:** Approved
**Author:** Architect (Claude), in collaboration with Overmind

## 1. Purpose

This is the first business module built on top of the platform skeleton (see
`docs/superpowers/specs/2026-09-03-forecourt-terminal-architecture-design.md` and
`docs/ARCHITECTURE.md`'s module ownership table). It claims `Core/Modules/AttendantMgmt/`.

Attendant Management is the most foundational of the four deferred modules: `Login.razor` and
the shift open/close pages are currently UI-only stubs specifically because there is no
signed-in attendant to act on behalf of. This module builds real PIN-based login, attendant
CRUD, and wires the signed-in attendant through to `ShiftService`, unblocking real shift
open/close.

## 2. Scope decisions

- **Wired through, not just login + CRUD.** `ShiftOpen.razor`/`ShiftClose.razor` are updated in
  this pass to resolve the signed-in attendant and call the real `ShiftService`, rather than
  leaving that as a separate follow-up.
- **PIN hashing via the BCL, not a new package.** `System.Security.Cryptography.Rfc2898DeriveBytes`
  (PBKDF2) — keeps `Core` dependency-free, and is the same primitive ASP.NET Core Identity's own
  `PasswordHasher` uses internally.
- **Session state persists to `TerminalDbContext`**, not an in-memory-only service — a terminal
  reboot mid-shift shouldn't force a re-login and orphan the in-progress shift.
- **Login is tap-your-name-then-PIN**, not a single PIN pad scanning every attendant. Matches how
  real forecourt PIN terminals work, and only ever does one PBKDF2 verification per attempt
  instead of up to N.
- **Attendant Management (CRUD) is Manager-only.** Enforced by checking the signed-in attendant's
  `AttendantRole` in the page itself (inline "Managers only" message, not a redirect — makes it
  obvious why the page looks the way it does rather than looking broken).
- **Attendants are deactivated, never hard-deleted.** `Shift`/`Sale` reference `AttendantId`;
  hard-deleting would violate that FK or destroy history.
- **A default Manager attendant is seeded** (Name "Manager", PIN `0000`) so a fresh install has
  someone who can log in and create real attendants. Documented in `README.md` as a known
  local/demo credential.
- **PINs are 4-6 numeric digits**, validated at the point of entry (login PIN pad, create/reset
  forms) — not stored or compared as anything but a hash. PINs are **not required to be unique**
  across attendants: because login always starts from picking a name, two attendants sharing the
  same PIN is not ambiguous (unlike the rejected PIN-only-scan approach, where it would have
  been).

## 3. Data model additions

`MAUI-POS-DASH.Core/Domain/Attendant.cs` gains one field:

```csharp
public bool IsActive { get; set; } = true;
```

New entity `MAUI-POS-DASH.Core/Domain/AttendantSession.cs`, mirroring `Shift`'s
`OpenedAt`/`ClosedAt` pattern already in the codebase:

```csharp
public class AttendantSession
{
    public Guid Id { get; set; }
    public Guid AttendantId { get; set; }
    public Attendant? Attendant { get; set; }
    public DateTimeOffset SignedInAt { get; set; }
    public DateTimeOffset? SignedOutAt { get; set; }
}
```

A row with `SignedOutAt == null` is the currently-active session. `AttendantSession` lives **only**
in `TerminalDbContext` — which attendant is using a specific physical terminal right now is
inherently local state, not something the back office needs synced to `BackofficeDbContext`.

This requires a new EF Core migration on `TerminalDbContext` on top of the existing
`InitialCreate` migration (adds the `IsActive` column to `Attendants` and the new
`AttendantSessions` table). `BackofficeDbContext` is unaffected — `AttendantSession` is not part
of its model.

## 4. Core services (`Core/Attendants/`, new folder, mirrors `Core/Shifts/`)

- **`IAttendantRepository`** — `GetActiveAttendantsAsync()`, `GetByIdAsync(Guid)`,
  `AddAsync(Attendant)`, `UpdateAsync(Attendant)`. Covers create, edit, deactivate, and PIN reset
  (all just attribute changes persisted through `UpdateAsync`).
- **`IPinHasher`** — `string Hash(string pin)`, `bool Verify(string pin, string hash)`. PBKDF2
  with a random salt packed into the returned hash string (no separate salt column needed).
- **`IAttendantSessionStore`** — `GetActiveSessionAsync()`, `SignInAsync(Guid attendantId)`,
  `SignOutAsync()`. EF implementation (`EfAttendantSessionStore`, in `Core.Persistence`) mirrors
  `EfShiftRepository`.
- **`AttendantService`** — the application service:
  - `TryLoginAsync(Guid attendantId, string pin)` → `LoginResult` (see §6), signs in via
    `IAttendantSessionStore` on success.
  - `LogoutAsync()` → `IAttendantSessionStore.SignOutAsync()`.
  - `CreateAttendantAsync(string name, string pin, AttendantRole role)`.
  - `UpdateAttendantAsync(Guid id, string name, AttendantRole role)`.
  - `ResetPinAsync(Guid id, string newPin)`.
  - `DeactivateAttendantAsync(Guid id)`.

`EfAttendantRepository` (Core.Persistence) implements `IAttendantRepository` against
`TerminalDbContext`, same pattern as `EfShiftRepository`.

## 5. Terminal UI

- **`Login.razor`** — two-step: a tile grid of active attendant names (reusing the existing
  `.module-grid`/`.module-tile` CSS), tap one to reveal a PIN pad scoped to that attendant. A
  wrong PIN shows an inline error and stays on the PIN step. A correct PIN calls
  `AttendantService.TryLoginAsync` and navigates to `/`.
- **`TerminalLayout.razor`** — the status bar gains the signed-in attendant's name and a "Sign
  out" action next to the existing connectivity indicator. Shows "Not signed in" when there's no
  active session.
- **`AttendantManagement.razor`** — Manager-only. Lists active attendants with edit/deactivate
  actions, plus a "New attendant" form (name, PIN, role). Editing an attendant includes an
  optional new-PIN field (blank keeps the existing PIN).
- **`ShiftOpen.razor`** — resolves the active session via `IAttendantSessionStore`; if none,
  redirects to `/login`. On submit, calls `ShiftService.OpenShiftAsync(attendantId, openingFloat)`
  instead of just displaying a message.
- **`ShiftClose.razor`** — same resolution; calls `ShiftService.CloseShiftAsync(shift,
  closingCashCounted)` using the shift found via the signed-in attendant's active shift.

**Scope of the login guard:** only `ShiftOpen.razor`/`ShiftClose.razor`/`AttendantManagement.razor`
check for a signed-in attendant in this pass (redirecting to `/login` — or, for
`AttendantManagement.razor`, showing the "Managers only" message — when there isn't one or the
role doesn't qualify). `Home.razor` and the other modules' "coming soon" pages
(`FleetCardSale.razor`, etc.) remain reachable without signing in — a global "every page requires
login" guard is a reasonable future addition but isn't part of this module's scope.

## 6. Error handling

Login failure (wrong PIN, inactive attendant) is expected user behavior, not an exceptional
condition — `TryLoginAsync` returns a `LoginResult` status enum
(`Success`/`InvalidPin`/`AttendantInactive`), not a thrown exception, so the UI shows an inline
message. This matches the existing pattern in the codebase: `ShiftService.OpenShiftAsync` throws
for a genuine contract violation (opening a shift twice), while `CardReadResult`/`SyncResult` use
status enums for expected-but-unhappy outcomes. A wrong PIN is squarely in the second category.

## 7. Testing

Same convention as the platform skeleton: NUnit, a seeded SQLite in-memory `TerminalDbContext`
fixture per test, `#region Arrange` / `#region Act` / `#region Assert`. Coverage:

- `IPinHasher`: hash-then-verify round trip, correct PIN, incorrect PIN.
- `AttendantService.TryLoginAsync`: correct PIN succeeds and creates an `AttendantSession`; wrong
  PIN fails with no session created; an inactive attendant cannot log in even with the right PIN.
- `AttendantService` CRUD paths: create, deactivate (attendant no longer appears in
  `GetActiveAttendantsAsync`), PIN reset (old PIN stops verifying, new one succeeds).

## 8. What this pass does not cover

- Idle/session timeout (auto sign-out after inactivity) — noted as a future concern, not solved
  here.
- Any UI for viewing session *history* (past sign-ins) — only the current active session matters
  for this pass.
- Photo/avatar tiles on the login grid — name-only tiles for now.
