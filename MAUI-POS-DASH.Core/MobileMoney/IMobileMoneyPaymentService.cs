namespace MAUI_POS_DASH.Core.MobileMoney;

public enum MobileMoneyPaymentStatus
{
    Confirmed,
    Declined,
    TimedOut
}

public record MobileMoneyPaymentResult(MobileMoneyPaymentStatus Status, string? DetailMessage);

/// <summary>
/// We shape this after IFleetCardAuthorizationService — a real mobile money provider adapter is a
/// drop-in implementation of this interface, not a rewrite of anything that calls it.
/// </summary>
public interface IMobileMoneyPaymentService
{
    Task<MobileMoneyPaymentResult> RequestPaymentAsync(decimal amount, CancellationToken cancellationToken = default);
}
