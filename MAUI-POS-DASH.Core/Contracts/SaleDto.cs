namespace MAUI_POS_DASH.Core.Contracts;

/// <summary>
/// We mirror Sale's lines; Total is computed here from the mapped Lines, not mapped itself.
/// </summary>
public record SaleDto(Guid Id, Guid ShiftId, DateTimeOffset OccurredAt, IReadOnlyList<SaleLineDto> Lines)
{
    public decimal Total => Lines.Sum(line => line.LineTotal);
}
