namespace MAUI_POS_DASH.Core.FleetCard;

public enum FleetCardAuthorizationStatus
{
    Approved,
    Declined
}

public record FleetCardAuthorizationResult(FleetCardAuthorizationStatus Status, string? DeclineReason);

/// <summary>
/// We shape this after ICardReaderService — a real fleet-card backend adapter is a drop-in
/// implementation of this interface, not a rewrite of anything that calls it.
/// </summary>
public interface IFleetCardAuthorizationService
{
    Task<FleetCardAuthorizationResult> AuthorizeAsync(decimal amount, CancellationToken cancellationToken = default);
}
