using MAUI_POS_DASH.Core.Sales;
using MAUI_POS_DASH.Core.Shifts;

namespace MAUI_POS_DASH.Core.Cash;

/// <summary>
/// We record a cash tender against a Sale and feed the shift's Till — no card, no network, just
/// arithmetic and persistence, so this is simpler than FleetCardSaleService by design.
/// </summary>
public class CashSaleService
{
    #region Fields
    private readonly ISaleRepository _saleRepository;
    private readonly ITillRepository _tillRepository;
    private readonly IShiftRepository _shiftRepository;
    #endregion

    #region Constructor
    public CashSaleService(ISaleRepository saleRepository, ITillRepository tillRepository, IShiftRepository shiftRepository)
    {
        _saleRepository = saleRepository;
        _tillRepository = tillRepository;
        _shiftRepository = shiftRepository;
    }
    #endregion

    #region Public Methods
    public async Task<CashSaleResult> ProcessSaleAsync(
        Guid shiftId,
        decimal amountOwed,
        decimal amountTendered,
        CancellationToken cancellationToken = default)
    {
        if (amountOwed <= 0)
        {
            throw new ArgumentException("A cash sale must have a positive amount owed.", nameof(amountOwed));
        }

        if (amountTendered < amountOwed)
        {
            throw new ArgumentException("Cash tendered must cover the amount owed.", nameof(amountTendered));
        }

        SalePreconditions.ValidateCurrencyAmount(amountOwed, nameof(amountOwed), "A cash sale amount owed");

        var till = await SalePreconditions.GetOpenShiftTillAsync(_shiftRepository, _tillRepository, shiftId, cancellationToken);

        var sale = new Sale
        {
            Id = Guid.NewGuid(),
            ShiftId = shiftId,
            OccurredAt = DateTimeOffset.UtcNow,
            Lines =
            [
                new SaleLine
                {
                    Id = Guid.NewGuid(),
                    Description = "Fuel",
                    UnitPrice = amountOwed,
                    Quantity = 1
                }
            ]
        };
        var transaction = new Transaction
        {
            Id = Guid.NewGuid(),
            SaleId = sale.Id,
            Method = PaymentMethod.Cash,
            Amount = amountOwed,
            Status = TransactionStatus.Pending,
            CreatedAt = DateTimeOffset.UtcNow
        };

        await _saleRepository.AddSaleWithTransactionAsync(sale, transaction, cancellationToken);

        till.CashTotal += amountOwed;
        await _tillRepository.UpdateAsync(till, cancellationToken);

        return new CashSaleResult(sale, transaction, amountTendered - amountOwed);
    }
    #endregion
}
