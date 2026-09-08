using System.Net.Http.Json;
using MAUI_POS_DASH.Core.Contracts;
using MAUI_POS_DASH.Core.Sync;

namespace MAUI_POS_DASH.Services;

/// <summary>
/// We post pending transactions to the back office's sync endpoint over HTTP. This is the one
/// piece that actually needs a real network connection — everything upstream of it (the sale,
/// the local write, the queue) already worked offline.
/// </summary>
public class HttpTransactionSyncService : ITransactionSyncService
{
    #region Fields
    private readonly HttpClient _httpClient;
    private readonly EntityMapper _mapper;
    #endregion

    #region Constructor
    public HttpTransactionSyncService(HttpClient httpClient, EntityMapper mapper)
    {
        _httpClient = httpClient;
        _mapper = mapper;
    }
    #endregion

    #region Public Methods
    public async Task<SyncResult> SyncAsync(IReadOnlyList<Transaction> pendingTransactions, CancellationToken cancellationToken = default)
    {
        // We build Sales from the pending transactions' already-loaded Sale navigation rather than
        // querying separately — GetPendingAsync eager-loads it precisely so this can stay one pass.
        var sales = pendingTransactions
            .Select(transaction => transaction.Sale)
            .Where(sale => sale is not null)
            .DistinctBy(sale => sale!.Id)
            .Select(sale => _mapper.ToDto(sale!))
            .ToList();
        var transactions = pendingTransactions.Select(_mapper.ToDto).ToList();
        var request = new TransactionSyncRequest(sales, transactions);

        try
        {
            var response = await _httpClient.PostAsJsonAsync("api/transactions", request, cancellationToken);
            return response.IsSuccessStatusCode
                ? new SyncResult(SyncStatus.Success, transactions.Count)
                : new SyncResult(SyncStatus.ServerRejected, 0);
        }
        catch (HttpRequestException)
        {
            // We treat a dropped connection as an expected condition on a forecourt, not an
            // error to surface — the queue simply retries next time TryFlushAsync runs.
            return new SyncResult(SyncStatus.NetworkUnavailable, 0);
        }
    }
    #endregion
}
