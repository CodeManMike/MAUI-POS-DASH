# Mobile Money module

**Status:** not started. **Owner:** unclaimed.

Handles mobile money payment for a fuel sale — a QR code or USSD prompt is shown, the customer
confirms on their phone, and the terminal polls (or is notified) that the payment cleared before
creating a `Transaction` with `PaymentMethod.MobileMoney`.

**Depends on:** `Core/Sync/ITransactionSyncService.cs` (mobile money confirmation is inherently a
network-round-trip, unlike cash or fleet card).

**To claim this module:** add your name to the Owner line above, update the row in
`docs/ARCHITECTURE.md`, and open a PR/branch scoped to this folder plus the
`MAUI-POS-DASH/Components/Pages/MobileMoneySale.razor` placeholder page.
