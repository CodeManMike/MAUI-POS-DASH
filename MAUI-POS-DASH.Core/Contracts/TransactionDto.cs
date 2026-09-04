namespace MAUI_POS_DASH.Core.Contracts;

/// <summary>
/// We mirror Transaction 1:1 — this is what crosses the wire to the sync endpoint and what the
/// dashboard reads back.
/// </summary>
public record TransactionDto(
    Guid Id,
    Guid SaleId,
    PaymentMethod Method,
    decimal Amount,
    TransactionStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? SyncedAt);
