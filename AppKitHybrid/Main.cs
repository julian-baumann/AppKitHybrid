using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AppKitHybrid;

internal class Program : IHostedService
{
    public static IServiceProvider ServiceProvider { get; private set; }

    private static async Task Main(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);
        //appBuilder.Logging.AddDebug();
        builder.Logging.AddSimpleConsole(
                options => {
                    options.ColorBehavior = Microsoft.Extensions.Logging.Console.LoggerColorBehavior.Disabled;
                    options.IncludeScopes = false;
                    options.SingleLine = true;
                    options.TimestampFormat = "hh:mm:ss ";
                })
            .SetMinimumLevel(LogLevel.Information);
        
        builder.Services
            .AddBlazorWebView()
            .AddSingleton(new BlazorWebViewOptions
            {
                RootComponent = typeof(BlazorApp.Components.App),
                ContentRoot = "wwwroot",
                RelativeHostPath = "wwwroot/index.html"
            })
            .AddHostedService<Program>();

        using var host = builder.Build();

        await host.RunAsync();
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