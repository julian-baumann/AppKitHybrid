using System.Runtime.Versioning;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.WebView;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;
using WebKit;

namespace AppKitHybrid;

[SupportedOSPlatform("macos")]
public sealed class BlazorWebViewOptions
{
    public required string ContentRoot { get; init; }
    // Host page inside ContentRoot, e.g., "/index.html"
    public required string RelativeHostPath { get; init; }
    // Your root Razor component, e.g., typeof(App)
    public required Type RootComponent { get; init; }
}

[SupportedOSPlatform("macos")]
public sealed class BlazorWebView : WKWebView
{
    public BlazorWebView(IServiceProvider services) : base(new CGRect(0, 0, 800, 600), NewConfig())
    {
        Services = services;
        Manager = new AppKitWebViewManager(this, services);
        AutoresizingMask = NSViewResizingMask.WidthSizable | NSViewResizingMask.HeightSizable;
    }

    public IServiceProvider Services { get; }
    public AppKitWebViewManager Manager { get; }

    static WKWebViewConfiguration NewConfig()
    {
        var cfg = new WKWebViewConfiguration
        {
            DefaultWebpagePreferences = new WKWebpagePreferences
            {
                AllowsContentJavaScript = true
            }
        };

        cfg.Preferences ??= new WKPreferences();
        cfg.Preferences.JavaScriptCanOpenWindowsAutomatically = true;

        return cfg;
    }
}

[SupportedOSPlatform("macos")]
public sealed class AppKitWebViewManager : WebViewManager
{
    sealed class ScriptHandler(AppKitWebViewManager owner) : WKScriptMessageHandler
    {
        AppKitWebViewManager Owner { get; } = owner;

        public override void DidReceiveScriptMessage(WKUserContentController userContentController, WKScriptMessage message)
        {
            var text = message.Body.ToString() ?? string.Empty;
            Owner.MessageReceived(BaseUri, text);
        }
    }

    sealed class SchemeHandler(AppKitWebViewManager owner) : NSObject, IWKUrlSchemeHandler
    {
        AppKitWebViewManager Owner { get; } = owner;

        [Export("webView:startURLSchemeTask:")]
        public void StartUrlSchemeTask(WKWebView webView, IWKUrlSchemeTask task)
        {
            var url = task.Request?.Url?.AbsoluteString ?? $"{Scheme}://localhost/";
            var path = task.Request?.Url?.Path ?? "/";

            if (path == "/")
            {
                url = $"{Scheme}://localhost{Owner.RelativeHostPath}";
            }

            if (Owner.TryGetResponseContent(url, false, out var status, out var statusText, out var content, out var headers))
            {
                using var ms = new MemoryStream();
                content.CopyTo(ms);

                var data = NSData.FromArray(ms.GetBuffer().AsSpan(0, (int)ms.Length).ToArray());
                var mime = headers.TryGetValue("Content-Type", out var ct) ? ct : "application/octet-stream";

                var response = new NSUrlResponse(task.Request!.Url!, mime, (nint)ms.Length, null);
                task.DidReceiveResponse(response);
                task.DidReceiveData(data);
                task.DidFinish();
            }
            else
            {
                var info = new NSMutableDictionary
                {
                    { new NSString("status"), new NSString(status.ToString()) },
                    { new NSString("message"), new NSString(statusText ?? string.Empty) }
                };

                var error = new NSError(new NSString("AppKitHybrid"), -1, info);
                task.DidFailWithError(error);
            }
        }

        [Export("webView:stopURLSchemeTask:")]
        public void StopUrlSchemeTask(WKWebView webView, IWKUrlSchemeTask task) { }
    }

    const string Scheme = "app";
    static readonly Uri BaseUri = new($"{Scheme}://localhost/");

    public AppKitWebViewManager(WKWebView webView, IServiceProvider services)
        : base(
            services,
            Dispatcher.CreateDefault(),
            BaseUri,
            CreateContentRootFileProvider(services),
            new(),
            services.GetRequiredService<BlazorWebViewOptions>().RelativeHostPath)
    {
        _webView = webView;
        _options = services.GetRequiredService<BlazorWebViewOptions>();
        _relativeHostPath = _options.RelativeHostPath;
        _logger = services.GetService<ILogger<AppKitWebViewManager>>();

        var ucc = _webView.Configuration.UserContentController ?? new WKUserContentController();
        _webView.Configuration.UserContentController = ucc;

        var bridge = """
                     window.__receiveMessageCallbacks = [];
                     window.__dispatchMessageCallback = function(m) { window.__receiveMessageCallbacks.forEach(c => c(m)); };
                     window.external = {
                         sendMessage: function(m) { window.webkit.messageHandlers.webview.postMessage(m); },
                         receiveMessage: function(cb) { window.__receiveMessageCallbacks.push(cb); }
                     };
                     """;

        var script = new WKUserScript(new NSString(bridge), WKUserScriptInjectionTime.AtDocumentStart, true);
        ucc.AddUserScript(script);
        ucc.AddScriptMessageHandler(new ScriptHandler(this), "webview");

        _webView.Configuration.SetUrlSchemeHandler(new SchemeHandler(this), Scheme);

        _ = Dispatcher.InvokeAsync(async () =>
        {
            await AddRootComponentAsync(_options.RootComponent, "#app", ParameterView.Empty);
            Navigate("/");
        });
    }

    static IFileProvider CreateContentRootFileProvider(IServiceProvider services)
    {
        var opts = services.GetRequiredService<BlazorWebViewOptions>();
        var baseDir = AppContext.BaseDirectory;
        var contentRoot = opts.ContentRoot;

        if (!Path.IsPathFullyQualified(contentRoot))
        {
            contentRoot = Path.GetFullPath(Path.Combine(baseDir, "..", "Resources", contentRoot));
        }

        if (Directory.Exists(contentRoot))
        {
            return new PhysicalFileProvider(contentRoot);
        }

        // Fallback: serve from embedded resources (wwwroot) of the entry assembly
        // var asm = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
        var asm = typeof(BlazorApp.Components.App).Assembly;
        return new ManifestEmbeddedFileProvider(asm, "wwwroot");
    }

    readonly WKWebView _webView;
    readonly BlazorWebViewOptions _options;
    readonly ILogger<AppKitWebViewManager>? _logger;
    readonly string _relativeHostPath;

    public string RelativeHostPath => _relativeHostPath;

    protected override void NavigateCore(Uri absoluteUri)
    {
        _logger?.LogDebug("Navigating {Url}", absoluteUri);
        _webView.LoadRequest(new NSUrlRequest(new NSUrl(absoluteUri.ToString())));
    }

    protected override async void SendMessage(string message)
    {
        var encoded = JavaScriptEncoder.Default.Encode(message);
        var js = $"__dispatchMessageCallback(\"{encoded}\")";
        _logger?.LogDebug("Dispatch {Script}", js);
        _ = await _webView.EvaluateJavaScriptAsync(js);
    }
}