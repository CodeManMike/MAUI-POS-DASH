using Riok.Mapperly.Abstractions;

namespace MAUI_POS_DASH.Core.Contracts;

/// <summary>
/// We generate entity&lt;-&gt;DTO mapping at compile time with Mapperly instead of AutoMapper —
/// AutoMapper moved to a commercial license in 2024, and Mapperly's source-generated output is
/// inspectable (check the generated .g.cs under obj/ if you want to see exactly what runs).
/// </summary>
[Mapper]
public partial class EntityMapper
{
    #region Attendant
    [MapperIgnoreSource(nameof(Attendant.PinHash))]
    public partial AttendantDto ToDto(Attendant attendant);
    #endregion

    #region Shift
    [MapperIgnoreSource(nameof(Shift.Till))]
    public partial ShiftDto ToDto(Shift shift);
    #endregion

    #region Till
    public partial TillDto ToDto(Till till);
    #endregion

    #region Sale
    [MapperIgnoreSource(nameof(SaleLine.SaleId))]
    public partial SaleLineDto ToDto(SaleLine line);

    public partial SaleDto ToDto(Sale sale);
    #endregion

    #region Transaction
    [MapperIgnoreSource(nameof(Transaction.Sale))]
    public partial TransactionDto ToDto(Transaction transaction);
    #endregion
}
