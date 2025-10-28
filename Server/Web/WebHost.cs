using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.FileProviders;

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
        builder.Logging.ClearProviders();
        builder.Logging.AddProvider(new TuiLoggerProvider());
        
        var app = builder.Build();
        app.UseCors();
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
            await context.Response.WriteAsync("Hello, World!");
        });
    }
}
