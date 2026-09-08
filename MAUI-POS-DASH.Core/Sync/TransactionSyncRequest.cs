using MAUI_POS_DASH.Core.Contracts;

namespace MAUI_POS_DASH.Core.Sync;

/// <summary>
/// We wrap both lists in one request type because Minimal API model binding only binds one
/// complex type from the JSON body — Sales and Transactions need to travel in a single request so
/// the sync stays one HTTP round trip and one atomic database write.
/// </summary>
public sealed record TransactionSyncRequest(IReadOnlyList<SaleDto> Sales, IReadOnlyList<TransactionDto> Transactions);
