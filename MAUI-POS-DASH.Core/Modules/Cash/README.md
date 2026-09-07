# Cash module

**Status:** in progress. **Owner:** Architect.

See `docs/superpowers/specs/2026-09-07-cash-module-design.md` for the design.

Handles cash tender and change calculation for a fuel sale, and feeds `Till.CashTotal` so
`TillReconciliationService` has something real to reconcile against at shift close.

**Depends on:** `Core/Shifts/TillReconciliationService.cs`, `Core/Domain/Till.cs`.

**To claim this module:** add your name to the Owner line above, update the row in
`docs/ARCHITECTURE.md`, and open a PR/branch scoped to this folder plus the
`MAUI-POS-DASH/Components/Pages/CashSale.razor` placeholder page.
