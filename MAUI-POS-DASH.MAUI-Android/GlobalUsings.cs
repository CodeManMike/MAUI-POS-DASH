// This is the Model half of this app's MVVM — Attendant, Shift, Sale, Transaction, and the rest
// of the domain model are deliberately NOT redefined in a local Models folder here. They live in
// MAUI-POS-DASH.Core so this app and the Blazor Hybrid app bind to the exact same Model classes
// instead of maintaining two copies of the same shapes. A few narrower Model types (FleetCardSaleResult,
// CashSaleResult, MobileMoneySaleResult, ReconciliationResult) live in their own Core.* module
// namespaces instead of Core.Domain, and are imported directly by the ViewModels that use them.
global using MAUI_POS_DASH.Core.Domain;
