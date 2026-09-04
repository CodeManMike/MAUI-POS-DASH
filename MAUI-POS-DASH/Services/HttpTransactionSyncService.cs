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
        var dtos = pendingTransactions.Select(_mapper.ToDto).ToList();

        try
        {
            var response = await _httpClient.PostAsJsonAsync("api/transactions", dtos, cancellationToken);
            return response.IsSuccessStatusCode
                ? new SyncResult(SyncStatus.Success, dtos.Count)
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
