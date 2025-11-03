using System.Net.WebSockets;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.FileProviders;
using Server.Network;

namespace Server.Web;

public static class WebHost
{
    public static Task<IHost> StartAsync(CancellationToken cancellationToken = default)
    {
        var builder = WebApplication.CreateBuilder();

        builder.Services.Configure<JsonOptions>(options =>
        {
            options.SerializerOptions.IncludeFields = true;
        });
        builder.Services.AddCors(options =>
        {
            options.AddDefaultPolicy(
                policy =>
                {
                    policy.AllowAnyOrigin()
                          .AllowAnyHeader()
                          .AllowAnyMethod();
                });
        });
        builder.Logging.ClearProviders();
        builder.Logging.AddProvider(new TuiLoggerProvider());
        
        var app = builder.Build();
        app.UseCors();
        app.UseWebSockets(new WebSocketOptions()
        {
            KeepAliveInterval = TimeSpan.FromSeconds(60),
        });
        app.UseStaticFiles(new StaticFileOptions()
        {
            FileProvider = new PhysicalFileProvider(
                            Path.Combine(Directory.GetCurrentDirectory(), "Web", "assets"))
        });
        MapApp(app);

        _ = app.RunAsync(cancellationToken);

        return Task.FromResult((IHost)app);
    }
    
    private static void MapApp(WebApplication app)
    {
        app.MapGet("/", async context =>
        {
            context.Response.ContentType = "text/html";
            await context.Response.SendFileAsync(Path.Combine(Directory.GetCurrentDirectory(), "Web", "assets", "index.html"));
        });
        app.MapGet("/ws", async context =>
        {
            if (context.WebSockets.IsWebSocketRequest)
            {
                var webSocket = await context.WebSockets.AcceptWebSocketAsync();
                await Manager.NewConnection(webSocket);
            }
            else
            {
                context.Response.StatusCode = 400;
            }
        });
    }
}
