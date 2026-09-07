using MAUI_POS_DASH.Core.Devices;
using MAUI_POS_DASH.Core.Sales;

namespace MAUI_POS_DASH.Core.FleetCard;

/// <summary>
/// We own the whole tap-card payment flow — waiting for the card, authorizing it, and recording
/// the Sale and Transaction on approval — so FleetCardSale.razor only has to present state.
/// </summary>
public class FleetCardSaleService
{
    #region Fields
    private readonly ICardReaderService _cardReader;
    private readonly IFleetCardAuthorizationService _authorizationService;
    private readonly ISaleRepository _saleRepository;
    #endregion

    #region Constructor
    public FleetCardSaleService(
        ICardReaderService cardReader,
        IFleetCardAuthorizationService authorizationService,
        ISaleRepository saleRepository)
    {
        _cardReader = cardReader;
        _authorizationService = authorizationService;
        _saleRepository = saleRepository;
    }
    #endregion

    #region Public Methods
    /// <summary>
    /// We validate the amount before ever waiting for a card tap — an attendant shouldn't have to
    /// tap a card only to be told the amount they typed was invalid.
    /// </summary>
    public async Task<FleetCardSaleResult> ProcessSaleAsync(Guid shiftId, decimal amount, CancellationToken cancellationToken = default)
    {
        if (amount <= 0)
        {
            throw new ArgumentException("A fleet card sale must have a positive amount.", nameof(amount));
        }

        CardReadResult cardRead = await _cardReader.WaitForCardAsync(cancellationToken);
        if (cardRead.Status != CardReadStatus.Success)
        {
            return new FleetCardSaleResult(
                FleetCardSaleStatus.CardReadFailed,
                Sale: null,
                Transaction: null,
                DetailMessage: DescribeCardReadFailure(cardRead.Status));
        }

        FleetCardAuthorizationResult authorization = await _authorizationService.AuthorizeAsync(amount, cancellationToken);
        if (authorization.Status == FleetCardAuthorizationStatus.Declined)
        {
            return new FleetCardSaleResult(
                FleetCardSaleStatus.Declined,
                Sale: null,
                Transaction: null,
                DetailMessage: authorization.DeclineReason ?? "Card declined by the fleet operator.");
        }

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
            Method = PaymentMethod.FleetCard,
            Amount = amount,
            Status = TransactionStatus.Pending,
            CreatedAt = DateTimeOffset.UtcNow
        };

        await _saleRepository.AddSaleWithTransactionAsync(sale, transaction, cancellationToken);

        return new FleetCardSaleResult(FleetCardSaleStatus.Approved, sale, transaction, "Payment approved.");
    }
    #endregion

    #region Private Methods
    private static string DescribeCardReadFailure(CardReadStatus status) => status switch
    {
        CardReadStatus.Timeout => "No card was presented in time.",
        CardReadStatus.DeviceUnavailable => "The card reader isn't available right now.",
        CardReadStatus.Cancelled => "The card read was cancelled.",
        _ => "The card couldn't be read."
    };
    #endregion
}
