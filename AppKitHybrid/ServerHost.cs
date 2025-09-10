using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.DependencyInjection;

namespace AppKitHybrid;

public sealed class ServerHost
{
    private WebApplication? _app;

    public async Task<string> StartAsync()
    {
        var builder = WebApplication.CreateBuilder(["--urls", "http://127.0.0.1:0"]);

        builder.Services
            .AddRazorComponents()
            .AddInteractiveServerComponents();

        var app = builder.Build();

        var webRoot = Path.Combine(AppContext.BaseDirectory, "wwwroot");
        if (Directory.Exists(webRoot))
        {
            app.Environment.WebRootPath = webRoot;
            app.Environment.WebRootFileProvider = new PhysicalFileProvider(webRoot);
        }

        app.UseHttpsRedirection();
        app.UseStaticFiles();
        app.UseAntiforgery();

        app.MapRazorComponents<BlazorApp.Components.App>()
            .AddInteractiveServerRenderMode();

        await app.StartAsync();

        _app = app;

        var url = app.Urls.First(u => u.StartsWith("http://", StringComparison.OrdinalIgnoreCase));
        return url;
    }

    public async Task StopAsync()
    {
        if (_app is null)
        {
            return;
        }

        await _app.StopAsync();
        await _app.DisposeAsync();
        _app = null;
    }
}