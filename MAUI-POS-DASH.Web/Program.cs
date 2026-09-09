using MAUI_POS_DASH.Web;
using MAUI_POS_DASH.Web.Api;
using MAUI_POS_DASH.Web.Components;

/// <summary>We configure and run the MAUI-POS-DASH back-office Web host.</summary>
public partial class Program
{
    private static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Services.AddBackofficeServices(builder.Configuration);

        var app = builder.Build();

        // Configure the HTTP request pipeline.
        if (app.Environment.IsDevelopment())
        {
            app.UseWebAssemblyDebugging();
        }
        else
        {
            app.UseExceptionHandler("/Error", createScopeForErrors: true);
            // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
            app.UseHsts();
        }
        app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
        app.UseHttpsRedirection();

        app.UseAntiforgery();

        app.MapStaticAssets();
        app.MapTransactionsApi();

        app.MapRazorComponents<App>()
            .AddInteractiveServerRenderMode()
            .AddInteractiveWebAssemblyRenderMode()
            .AddAdditionalAssemblies(
                typeof(MAUI_POS_DASH.Shared._Imports).Assembly,
                typeof(MAUI_POS_DASH.Web.Client._Imports).Assembly);

        app.Run();
    }
}
