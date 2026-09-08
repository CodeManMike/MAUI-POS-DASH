using MAUI_POS_DASH.Core.Devices;

namespace MAUI_POS_DASH.MAUI_Android.Platforms.Android.Devices;

/// <summary>
/// We stand in for the real PAX card reader SDK until hardware is available. The shape mirrors
/// PAX's async/callback style so swapping in the real adapter later is a one-file change — no
/// caller of ICardReaderService needs to know the difference.
/// </summary>
public class SimulatedPaxCardReader : ICardReaderService
{
    #region Events
    public event EventHandler<CardTapEventArgs>? CardPresented;
    #endregion

    #region Public Methods
    public async Task<CardReadResult> WaitForCardAsync(CancellationToken cancellationToken = default)
    {
        // We fake a two-second tap delay so the terminal UI has something realistic to show
        // while it waits, without needing real PAX hardware attached.
        await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);

        const string maskedPan = "**** **** **** 4242";
        CardPresented?.Invoke(this, new CardTapEventArgs { MaskedPan = maskedPan, PresentedAt = DateTimeOffset.UtcNow });

        return new CardReadResult(CardReadStatus.Success, maskedPan, TrackData: null);
    }
    #endregion
}
