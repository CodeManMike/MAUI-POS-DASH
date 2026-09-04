namespace MAUI_POS_DASH.Core.Devices;

public enum CardReadStatus
{
    Success,
    Timeout,
    DeviceUnavailable,
    Cancelled
}

public record CardReadResult(CardReadStatus Status, string? MaskedPan, string? TrackData);

public class CardTapEventArgs : EventArgs
{
    public required string MaskedPan { get; init; }

    public required DateTimeOffset PresentedAt { get; init; }
}

/// <summary>
/// We shape this after PAX's real card reader SDKs — an awaitable read plus a fire-and-forget
/// presented event — so a real PAX adapter is a drop-in implementation of this interface rather
/// than a rewrite of anything that calls it.
/// </summary>
public interface ICardReaderService
{
    event EventHandler<CardTapEventArgs>? CardPresented;

    Task<CardReadResult> WaitForCardAsync(CancellationToken cancellationToken = default);
}
