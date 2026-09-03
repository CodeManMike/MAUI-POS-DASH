namespace MAUI_POS_DASH.Web.Api;

public static class TransactionsApi
{
    #region Public Methods
    /// <summary>
    /// We expose the sync endpoint terminals post pending transactions to. This proves the
    /// contract compiles end to end — the actual persist-to-Postgres logic is left for whoever
    /// picks up the Sync side of the back office (see docs/ARCHITECTURE.md).
    /// </summary>
    public static IEndpointRouteBuilder MapTransactionsApi(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/transactions", (List<TransactionDto> transactions) =>
        {
            // TODO(Builder): persist these to BackofficeDbContext and return per-transaction results.
            return Results.StatusCode(StatusCodes.Status501NotImplemented);
        });

        return app;
    }
    #endregion
}
