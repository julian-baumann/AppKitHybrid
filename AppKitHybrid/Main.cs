using System;
using System.IO;
using System.Linq;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AppKitHybrid;

internal class Program : IHostedService
{
    private static string GetResourcesPath()
    {
        var exePath = AppContext.BaseDirectory;
        var contentsDir = Directory.GetParent(exePath)?.FullName;
        return contentsDir is null
            ? throw new InvalidOperationException("Unable to locate .app Contents directory.")
            : Path.Combine(contentsDir, "Resources");
    }

    private static string GetWwwRoot() => Path.Combine(GetResourcesPath(), "wwwroot");
    
    public static IServiceProvider ServiceProvider { get; private set; } = null!;
    
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Logging.ClearProviders();
        builder.Logging.AddConsole();
        
        builder.Services.AddBlazorWebViewOptions(
            new BlazorWebViewOptions
            {
                RootComponent = typeof(BlazorApp.Components.App),
                HostPath = "wwwroot/index.html"
            }
        );
        
        builder.Services
            .AddHostedService<Program>()
            .AddRazorComponents();

        var app = builder.Build();

        if (app.Environment.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }

        app.MapRazorComponents<BlazorApp.Components.App>();
        // app.MapFallbackToFile("index.html");

        // Bind to a free loopback port
        // app.Urls.Clear();
        // app.Urls.Add("http://127.0.0.1:0");
        ServiceProvider = app.Services;
        await app.RunAsync();
        //
        // // Resolve the actual bound URL (e.g., http://127.0.0.1:51234)
        // var baseUrl = app.Urls.First();

        // Start the Cocoa app and show the webview

        await app.StopAsync();
    }

    public Program(IHostApplicationLifetime lifetime, IServiceProvider serviceProvider)
    {
        NSApplication.Init();
        NSApplication.SharedApplication.Delegate = new AppDelegate();
        NSApplication.Main(["-NSQuitAlwaysKeepsWindows", "NO"]);

        // lifetime.ApplicationStarted.Register(() => 
        // {
        //     Task.Run(() => 
        //     {
        //         Environment.ExitCode = _app.Run(0, []);
        //     });
        // });
        //
        // lifetime.ApplicationStopping.Register(() =>
        // {
        //     _app.Quit();
        // });
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