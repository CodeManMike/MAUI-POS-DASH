namespace MAUI_POS_DASH.Platforms.Android.Devices;

/// <summary>
/// We stand in for the real PAX receipt printer SDK until hardware is available.
/// </summary>
public class SimulatedPaxReceiptPrinter : IReceiptPrinterService
{
    #region Public Methods
    public Task<PrintResult> PrintAsync(ReceiptDocument document, CancellationToken cancellationToken = default)
    {
        // We write to the debug console instead of driving a real ESC/POS printer — the document
        // shape here is exactly what the real PAX printer adapter will consume.
        foreach (var line in document.Lines)
        {
            System.Diagnostics.Debug.WriteLine(line.Text);
        }

        return Task.FromResult(new PrintResult(PrintStatus.Success));
    }
    #endregion
}
