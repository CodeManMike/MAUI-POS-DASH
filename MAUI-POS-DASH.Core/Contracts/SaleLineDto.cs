namespace MAUI_POS_DASH.Core.Contracts;

/// <summary>
/// We mirror SaleLine's priced fields; LineTotal is computed here rather than mapped.
/// </summary>
public record SaleLineDto(Guid Id, string Description, decimal UnitPrice, decimal Quantity)
{
    public decimal LineTotal => UnitPrice * Quantity;
}
