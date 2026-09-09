using MAUI_POS_DASH.Core.Sales;
using MAUI_POS_DASH.Core.Shifts;

namespace MAUI_POS_DASH.Core.MobileMoney;

/// <summary>
/// We own the whole confirmation-based payment flow — requesting confirmation and recording the
/// Sale, Transaction, and Till update on success — so MobileMoneySale.razor only presents state.
/// </summary>
public class MobileMoneySaleService
{
    #region Fields
    private readonly IMobileMoneyPaymentService _paymentService;
    private readonly ISaleRepository _saleRepository;
    private readonly ITillRepository _tillRepository;
    private readonly IShiftRepository _shiftRepository;
    #endregion

    #region Constructor
    public MobileMoneySaleService(
        IMobileMoneyPaymentService paymentService,
        ISaleRepository saleRepository,
        ITillRepository tillRepository,
        IShiftRepository shiftRepository)
    {
        _paymentService = paymentService;
        _saleRepository = saleRepository;
        _tillRepository = tillRepository;
        _shiftRepository = shiftRepository;
    }
    #endregion

    #region Public Methods
    public async Task<MobileMoneySaleResult> ProcessSaleAsync(Guid shiftId, decimal amount, CancellationToken cancellationToken = default)
    {
        if (amount <= 0)
        {
            throw new ArgumentException("A mobile money sale must have a positive amount.", nameof(amount));
        }
        SalePreconditions.ValidateCurrencyAmount(amount, nameof(amount), "A mobile money sale amount");

        MobileMoneyPaymentResult payment = await _paymentService.RequestPaymentAsync(amount, cancellationToken);
        if (payment.Status == MobileMoneyPaymentStatus.Declined)
        {
            return new MobileMoneySaleResult(
                MobileMoneySaleStatus.Declined, Sale: null, Transaction: null,
                DetailMessage: payment.DetailMessage ?? "The customer declined the payment.");
        }

        if (payment.Status == MobileMoneyPaymentStatus.TimedOut)
        {
            return new MobileMoneySaleResult(
                MobileMoneySaleStatus.TimedOut, Sale: null, Transaction: null,
                DetailMessage: payment.DetailMessage ?? "The customer didn't confirm in time.");
        }

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
                    UnitPrice = amount,
                    Quantity = 1
                }
            ]
        };
        var transaction = new Transaction
        {
            Id = Guid.NewGuid(),
            SaleId = sale.Id,
            Method = PaymentMethod.MobileMoney,
            Amount = amount,
            Status = TransactionStatus.Pending,
            CreatedAt = DateTimeOffset.UtcNow
        };

        await _saleRepository.AddSaleWithTransactionAsync(sale, transaction, cancellationToken);

        till.MobileMoneyTotal += amount;
        await _tillRepository.UpdateAsync(till, cancellationToken);

        return new MobileMoneySaleResult(MobileMoneySaleStatus.Confirmed, sale, transaction, "Payment confirmed.");
    }
    #endregion
}
