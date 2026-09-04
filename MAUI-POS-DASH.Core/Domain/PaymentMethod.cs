namespace MAUI_POS_DASH.Core.Domain;

/// <summary>
/// We record how a Transaction was tendered — the three ways this terminal accepts payment.
/// </summary>
public enum PaymentMethod
{
    Cash,
    FleetCard,
    MobileMoney
}
