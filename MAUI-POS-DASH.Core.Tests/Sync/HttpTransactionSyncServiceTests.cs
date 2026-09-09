using System.Net;
using System.Text.Json;
using MAUI_POS_DASH.Core.Contracts;
using MAUI_POS_DASH.Core.Sync;

namespace MAUI_POS_DASH.Core.Tests.Sync;

[TestFixture]
public class HttpTransactionSyncServiceTests
{
    #region Fields
    private static readonly JsonSerializerOptions s_deserializeOptions = new() { PropertyNameCaseInsensitive = true };
    #endregion

    #region Tests
    [Test]
    public async Task SyncAsync_ServerRespondsOk_ReturnsSuccessWithTransactionCount()
    {
        #region Arrange
        var handler = new FakeHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://unit-test.local/") };
        var sut = new HttpTransactionSyncService(httpClient, new EntityMapper());
        var transactions = new List<Transaction> { CreateTransaction(CreateSale()), CreateTransaction(CreateSale()) };
        #endregion

        #region Act
        SyncResult result = await sut.SyncAsync(transactions);
        #endregion

        #region Assert
        Assert.Multiple(() =>
        {
            Assert.That(result.Status, Is.EqualTo(SyncStatus.Success));
            Assert.That(result.TransactionsSynced, Is.EqualTo(transactions.Count));
        });
        #endregion
    }

    [Test]
    public async Task SyncAsync_ServerRespondsConflict_ReturnsServerRejectedWithZeroCount()
    {
        #region Arrange
        var handler = new FakeHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.Conflict));
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://unit-test.local/") };
        var sut = new HttpTransactionSyncService(httpClient, new EntityMapper());
        var transactions = new List<Transaction> { CreateTransaction(CreateSale()) };
        #endregion

        #region Act
        SyncResult result = await sut.SyncAsync(transactions);
        #endregion

        #region Assert
        Assert.Multiple(() =>
        {
            Assert.That(result.Status, Is.EqualTo(SyncStatus.ServerRejected));
            Assert.That(result.TransactionsSynced, Is.Zero);
        });
        #endregion
    }

    [Test]
    public async Task SyncAsync_HandlerThrowsHttpRequestException_ReturnsNetworkUnavailable()
    {
        #region Arrange
        // This is the "forecourt lost connectivity" case the class's doc comment describes — the
        // method must catch it rather than let it propagate up through TryFlushAsync.
        var handler = new FakeHttpMessageHandler(_ => throw new HttpRequestException("connection refused"));
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://unit-test.local/") };
        var sut = new HttpTransactionSyncService(httpClient, new EntityMapper());
        var transactions = new List<Transaction> { CreateTransaction(CreateSale()) };
        #endregion

        #region Act
        SyncResult result = await sut.SyncAsync(transactions);
        #endregion

        #region Assert
        Assert.Multiple(() =>
        {
            Assert.That(result.Status, Is.EqualTo(SyncStatus.NetworkUnavailable));
            Assert.That(result.TransactionsSynced, Is.Zero);
        });
        #endregion
    }

    [Test]
    public async Task SyncAsync_TwoTransactionsShareOneSale_PostsThatSaleOnlyOnce()
    {
        #region Arrange
        var handler = new FakeHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://unit-test.local/") };
        var sut = new HttpTransactionSyncService(httpClient, new EntityMapper());

        // Two transactions against the same Sale — a split-tender-adjacent scenario — must not
        // duplicate the Sale in the outgoing payload.
        var sale = CreateSale();
        var transactions = new List<Transaction>
        {
            CreateTransaction(sale, PaymentMethod.Cash),
            CreateTransaction(sale, PaymentMethod.FleetCard)
        };
        #endregion

        #region Act
        await sut.SyncAsync(transactions);
        #endregion

        #region Assert
        var postedRequest = JsonSerializer.Deserialize<TransactionSyncRequest>(handler.LastRequestBody!, s_deserializeOptions);
        Assert.That(postedRequest, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(postedRequest!.Sales, Has.Count.EqualTo(1));
            Assert.That(postedRequest.Sales[0].Id, Is.EqualTo(sale.Id));
            Assert.That(postedRequest.Transactions, Has.Count.EqualTo(2));
        });
        #endregion
    }
    #endregion

    #region Private Methods
    private static Sale CreateSale() => new()
    {
        Id = Guid.NewGuid(),
        ShiftId = Guid.NewGuid(),
        OccurredAt = DateTimeOffset.UtcNow,
        Lines = [new SaleLine { Id = Guid.NewGuid(), Description = "Fuel", UnitPrice = 250.00m, Quantity = 1 }]
    };

    private static Transaction CreateTransaction(Sale sale, PaymentMethod method = PaymentMethod.Cash) => new()
    {
        Id = Guid.NewGuid(),
        SaleId = sale.Id,
        Sale = sale,
        Method = method,
        Amount = 125.00m,
        Status = TransactionStatus.Pending,
        CreatedAt = DateTimeOffset.UtcNow
    };
    #endregion

    #region Fakes
    /// <summary>
    /// We stand in for the real network so SyncAsync's branches (success, rejection, dropped
    /// connection) are each reachable deterministically, and so we can inspect exactly what was
    /// posted.
    /// </summary>
    private sealed class FakeHttpMessageHandler : HttpMessageHandler
    {
        #region Fields
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;
        #endregion

        #region Properties
        public string? LastRequestBody { get; private set; }
        #endregion

        #region Constructor
        public FakeHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
        {
            _responder = responder;
        }
        #endregion

        #region Protected Methods
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequestBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return _responder(request);
        }
        #endregion
    }
    #endregion
}
