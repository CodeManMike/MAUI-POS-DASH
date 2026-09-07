namespace MAUI_POS_DASH.Core.FleetCard;

public enum FleetCardSaleStatus
{
    Approved,
    Declined,
    CardReadFailed
}

public record FleetCardSaleResult(FleetCardSaleStatus Status, Sale? Sale, Transaction? Transaction, string DetailMessage);
