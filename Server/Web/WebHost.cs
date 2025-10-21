using System.Numerics;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Builder;
using Server.Web.Pages;
using MudBlazor.Services;

namespace Server.Web;

public static class WebHost
{
    public static Task<IHost> StartAsync(CancellationToken cancellationToken = default)
    {
        //TODO: Redo all this shit from 0
        var builder = WebApplication.CreateBuilder();

        // Enable Razor Pages and Server-side Blazor. Pages are located in the project's default "Pages" folder.
        builder.Services.AddRazorPages();
        builder.Services.AddServerSideBlazor();
        builder.Services.AddRazorComponents()
            .AddInteractiveServerComponents()
            .AddInteractiveWebAssemblyComponents();
        builder.Services.AddMudServices();
        builder.Services.Configure<JsonOptions>(options =>
        {
            options.SerializerOptions.IncludeFields = true;
        });

        // Route ASP.NET Core logs into the TUI Web logger for visibility in the console UI
        builder.Logging.ClearProviders();
        builder.Logging.AddProvider(new TuiLoggerProvider());

        var app = builder.Build();

        app.UseStaticFiles(new StaticFileOptions()
        {
            FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(
                            Path.Combine(Directory.GetCurrentDirectory(), "Web"))
        });

        app.UseRouting();

        app.UseBlazorFrameworkFiles();

        // If the project registers an antiforgery middleware extension, keep it in the pipeline.
        // This call exists in the codebase already; leave it so antiforgery behavior remains unchanged.
        app.UseAntiforgery();

        // Register endpoints through UseEndpoints so EndpointMiddleware is added to the pipeline.
        app.UseEndpoints(endpoints =>
        {
            endpoints.MapRazorPages();
            endpoints.MapRazorComponents<App>()
                .AddInteractiveServerRenderMode()
                .AddInteractiveWebAssemblyRenderMode();
            endpoints.MapBlazorHub();

            // Health endpoint as a convenience
            endpoints.MapGet("/health", () => Results.Ok(new { status = "ok" }));
        });
        // No fallback mapping — the Razor page at '/' (Server/Pages/_Host.cshtml) will be served directly.

        // Start the web host in background; RunAsync returns a Task which completes on shutdown.
        _ = app.RunAsync(cancellationToken);

        // Return the built application as an IHost so caller can StopAsync if desired.
        return Task.FromResult((IHost)app);
    }
}
