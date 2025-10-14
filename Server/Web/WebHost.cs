using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Server.Web;

public static class WebHost
{
    public static async Task<IHost> StartAsync(CancellationToken cancellationToken = default)
    {
        var builder = WebApplication.CreateBuilder();

        // Enable Razor Pages and Server-side Blazor
        builder.Services.AddRazorPages();
        builder.Services.AddServerSideBlazor();

        var app = builder.Build();

        app.UseStaticFiles();
        app.UseRouting();

        app.MapRazorPages();
        app.MapBlazorHub();
        app.MapFallbackToPage("/_Host");

        // Health endpoint as a convenience
        app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

        // Start the web host in background; RunAsync returns a Task which completes on shutdown.
        _ = app.RunAsync(cancellationToken);

        // Return the built application as an IHost so caller can StopAsync if desired.
        return app;
    }
}
