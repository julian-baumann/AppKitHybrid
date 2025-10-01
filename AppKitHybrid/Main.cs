using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.StaticWebAssets;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AppKitHybrid;

internal class Program : IHostedService
{
    public static IServiceProvider ServiceProvider { get; private set; }
    
    private static string GetResourcePath(string resourceFileName)
    {
        var exePath = AppContext.BaseDirectory;

        var contentsDir = Directory.GetParent(exePath)?.FullName;
        return contentsDir is null
            ? throw new InvalidOperationException("Unable to locate .app Contents directory.")
            : Path.Combine(contentsDir, "Resources", resourceFileName);
    }

    private static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // StaticWebAssetsLoader.UseStaticWebAssets(builder.Environment, builder.Configuration);

        builder.Logging.AddSimpleConsole(
                options => {
                    options.ColorBehavior = Microsoft.Extensions.Logging.Console.LoggerColorBehavior.Disabled;
                    options.IncludeScopes = false;
                    options.SingleLine = true;
                    options.TimestampFormat = "hh:mm:ss ";
                })
            .SetMinimumLevel(LogLevel.Information);

        builder.Services
            .AddRazorComponents()
            .AddInteractiveServerComponents();

        builder.Services
            .AddBlazorWebView()
            .AddHostedService<Program>();

        await using var app = builder.Build();

        app.MapRazorComponents<BlazorApp.Components.App>()
            .AddInteractiveServerRenderMode();

        await app.RunAsync();
    }

    public Program(IHostApplicationLifetime lifetime, IServiceProvider serviceProvider)
    {
        ServiceProvider = serviceProvider;

        NSApplication.Init();
        NSApplication.SharedApplication.Delegate = new AppDelegate();
        NSApplication.Main([ "-NSQuitAlwaysKeepsWindows", "NO" ]);
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
