namespace MAUI_POS_DASH.Platforms.Android.Devices;

/// <summary>
/// We stand in for the real PAX barcode scanner SDK until hardware is available.
/// </summary>
public class SimulatedPaxBarcodeScanner : IBarcodeScannerService
{
    #region Events
    public event EventHandler<BarcodeScannedEventArgs>? BarcodeScanned;
    #endregion

    #region Public Methods
    /// <summary>
    /// We expose this so a future demo UI can trigger a fake scan — the real PAX adapter raises
    /// BarcodeScanned from the hardware scanner's own callback instead.
    /// </summary>
    public void SimulateScan(string value)
    {
        BarcodeScanned?.Invoke(this, new BarcodeScannedEventArgs { Value = value, ScannedAt = DateTimeOffset.UtcNow });
    }
    #endregion
}
