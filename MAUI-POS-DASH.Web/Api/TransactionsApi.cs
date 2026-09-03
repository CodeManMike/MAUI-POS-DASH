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
            // We return a Problem body rather than a bare status code — an empty-bodied 4xx/5xx
            // response gets intercepted by UseStatusCodePagesWithReExecute (see Program.cs) and
            // re-executed against /not-found with this request's original POST body still
            // attached, which then fails trying to parse it as a form post instead of JSON.
            return TypedResults.Problem(statusCode: StatusCodes.Status501NotImplemented, detail: "Transaction sync is not implemented yet.");
        })
        // We disable antiforgery here on purpose — this endpoint is called by the MAUI terminal's
        // plain HttpClient, which has no browser session to carry an antiforgery token in the
        // first place. CSRF protection doesn't apply to a machine-to-machine sync call.
        .DisableAntiforgery();

        return app;
    }
    #endregion
}
