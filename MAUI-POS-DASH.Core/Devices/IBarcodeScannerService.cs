namespace MAUI_POS_DASH.Core.Devices;

public class BarcodeScannedEventArgs : EventArgs
{
    public required string Value { get; init; }

    public required DateTimeOffset ScannedAt { get; init; }
}

/// <summary>
/// We model the scanner as a plain event source, matching how PAX's hardware scanner callback
/// works — there's no "wait for a scan" call, just a stream of scan events while active.
/// </summary>
public interface IBarcodeScannerService
{
    event EventHandler<BarcodeScannedEventArgs>? BarcodeScanned;
}
