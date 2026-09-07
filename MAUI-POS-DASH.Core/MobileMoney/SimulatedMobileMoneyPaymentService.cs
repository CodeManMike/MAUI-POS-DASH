namespace MAUI_POS_DASH.Core.MobileMoney;

/// <summary>
/// We stand in for a real mobile money provider until one exists. A confirmation takes a
/// simulated few seconds — a customer checking their phone genuinely takes longer than a card
/// tap — and resolves to one of three outcomes so the terminal has real decline and timeout paths
/// to demonstrate without a real provider to trigger them against.
/// </summary>
public class SimulatedMobileMoneyPaymentService : IMobileMoneyPaymentService
{
    #region Fields
    private const double TimeoutChance = 0.15;
    private const double DeclineChance = 0.35;
    private readonly Random _random;
    private readonly TimeSpan _confirmationDelay;
    #endregion

    #region Constructor
    public SimulatedMobileMoneyPaymentService(Random random, TimeSpan confirmationDelay)
    {
        _random = random;
        _confirmationDelay = confirmationDelay;
    }
    #endregion

    #region Public Methods
    public async Task<MobileMoneyPaymentResult> RequestPaymentAsync(decimal amount, CancellationToken cancellationToken = default)
    {
        await Task.Delay(_confirmationDelay, cancellationToken);

        double roll = _random.NextDouble();
        if (roll < TimeoutChance)
        {
            return new MobileMoneyPaymentResult(MobileMoneyPaymentStatus.TimedOut, "The customer didn't confirm in time.");
        }

        if (roll < DeclineChance)
        {
            return new MobileMoneyPaymentResult(MobileMoneyPaymentStatus.Declined, "The customer declined the payment.");
        }

        return new MobileMoneyPaymentResult(MobileMoneyPaymentStatus.Confirmed, null);
    }
    #endregion
}
