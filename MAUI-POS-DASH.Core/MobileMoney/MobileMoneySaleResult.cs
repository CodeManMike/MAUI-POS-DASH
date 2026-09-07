namespace MAUI_POS_DASH.Core.MobileMoney;

public enum MobileMoneySaleStatus
{
    Confirmed,
    Declined,
    TimedOut
}

public record MobileMoneySaleResult(MobileMoneySaleStatus Status, Sale? Sale, Transaction? Transaction, string DetailMessage);
