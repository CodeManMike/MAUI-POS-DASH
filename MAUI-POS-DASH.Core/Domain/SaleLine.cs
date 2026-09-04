namespace MAUI_POS_DASH.Core.Domain;

/// <summary>
/// We represent one priced line within a Sale — a fuel grade or a shop product, quantity and
/// unit price, with the line total computed rather than stored.
/// </summary>
public class SaleLine
{
    #region Properties
    public Guid Id { get; set; }

    public Guid SaleId { get; set; }

    public required string Description { get; set; }

    public decimal UnitPrice { get; set; }

    public decimal Quantity { get; set; }

    public decimal LineTotal => UnitPrice * Quantity;
    #endregion
}
