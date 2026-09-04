namespace MAUI_POS_DASH.Core.Devices;

public enum PrintStatus
{
    Success,
    DeviceUnavailable,
    OutOfPaper
}

public record PrintResult(PrintStatus Status);

public record ReceiptLine(string Text, bool Bold = false, bool CenterAligned = false);

/// <summary>
/// We use a plain line-based document, matching how ESC/POS printers (what PAX devices use)
/// actually render — no rich layout, just ordered lines with a couple of style flags.
/// </summary>
public record ReceiptDocument(IReadOnlyList<ReceiptLine> Lines);

/// <summary>
/// We shape this after PAX's ESC/POS-style printer SDK — a document of ordered lines in, a
/// result out — so a real PAX adapter is a drop-in implementation.
/// </summary>
public interface IReceiptPrinterService
{
    Task<PrintResult> PrintAsync(ReceiptDocument document, CancellationToken cancellationToken = default);
}
