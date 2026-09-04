# Attendant Management module

**Status:** done. **Owner:** Architect.

Handles attendant PIN login (tap a name, then enter PIN), PIN hashing/verification (PBKDF2 via
`Core/Attendants/Pbkdf2PinHasher.cs`), and CRUD for attendant records. Wired into
`ShiftOpen.razor`/`ShiftClose.razor` so shift open/close use the real signed-in attendant instead
of a placeholder message.

See `docs/superpowers/specs/2026-09-04-attendant-mgmt-design.md` for the full design.
