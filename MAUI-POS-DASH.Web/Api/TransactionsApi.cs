using MAUI_POS_DASH.Core.Sync;

namespace MAUI_POS_DASH.Web.Api;

/// <summary>We map transaction-sync HTTP requests onto the ingestion service.</summary>
public static class TransactionsApi
{
    #region Public Methods
    /// <summary>We expose the machine-to-machine endpoint used by terminal sync clients.</summary>
    public static IEndpointRouteBuilder MapTransactionsApi(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/transactions", IngestTransactionsAsync)
            // The MAUI HttpClient has no browser antiforgery token; terminal authentication is a
            // separate follow-up boundary and antiforgery does not secure machine clients.
            .DisableAntiforgery();

        return app;
    }
    #endregion

    #region Private Methods
    private static async Task<IResult> IngestTransactionsAsync(
        TransactionSyncRequest request,
        ITransactionIngestionService ingestionService,
        CancellationToken cancellationToken)
    {
        TransactionIngestionResult result =
            await ingestionService.IngestAsync(request.Sales, request.Transactions, cancellationToken);

        return result.Status switch
        {
            TransactionIngestionStatus.Success => TypedResults.Ok(result.Items),
            TransactionIngestionStatus.InvalidRequest => TypedResults.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Invalid transaction batch",
                detail: result.Detail),
            TransactionIngestionStatus.MissingSale => TypedResults.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Referenced sale is missing",
                detail: WithIds(result.Detail, result.OffendingIds)),
            TransactionIngestionStatus.TransactionConflict => TypedResults.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Transaction identity conflict",
                detail: WithIds(result.Detail, result.OffendingIds)),
            TransactionIngestionStatus.PersistenceConflict => TypedResults.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Transaction persistence conflict",
                detail: WithIds(result.Detail, result.OffendingIds)),
            _ => TypedResults.Problem(
                statusCode: StatusCodes.Status500InternalServerError,
                title: "Unknown transaction ingestion result",
                detail: "The ingestion service returned an unsupported outcome.")
        };
    }

    private static string WithIds(string? detail, IReadOnlyList<Guid> ids)
    {
        string prefix = detail ?? "The transaction batch could not be accepted.";
        return ids.Count == 0
            ? prefix
            : $"{prefix} IDs: {string.Join(", ", ids)}.";
    }
    #endregion
}
