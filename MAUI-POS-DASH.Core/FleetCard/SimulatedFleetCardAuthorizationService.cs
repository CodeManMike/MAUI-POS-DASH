namespace MAUI_POS_DASH.Core.FleetCard;

/// <summary>
/// We stand in for a real fleet-card backend until one exists. About one in five authorizations
/// is declined, so the terminal UI has a real decline path to demonstrate without needing a real
/// backend to simulate declining against.
/// </summary>
public class SimulatedFleetCardAuthorizationService : IFleetCardAuthorizationService
{
    #region Fields
    private const double DeclineChance = 0.2;
    private readonly Random _random;
    #endregion

    #region Constructor
    public SimulatedFleetCardAuthorizationService(Random random)
    {
        _random = random;
    }
    #endregion

    #region Public Methods
    public Task<FleetCardAuthorizationResult> AuthorizeAsync(decimal amount, CancellationToken cancellationToken = default)
    {
        bool declined = _random.NextDouble() < DeclineChance;
        var result = declined
            ? new FleetCardAuthorizationResult(FleetCardAuthorizationStatus.Declined, "Card declined by the fleet operator.")
            : new FleetCardAuthorizationResult(FleetCardAuthorizationStatus.Approved, null);

        return Task.FromResult(result);
    }
    #endregion
}
