using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.WebView;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;
using WebKit;

namespace AppKitHybrid;

public sealed class AppKitWebViewManager : WebViewManager
{
    public static readonly Uri BaseUri = new("app://localhost/");

    private readonly WKWebView _webView;
    private readonly ILogger? _logger;

    public string HostPageRelativePath { get; }

    public AppKitWebViewManager(
        WKWebView webView,
        IServiceProvider services,
        IFileProvider contentRoot,
        string relativeHostPath,
        Type rootComponentType,
        ILogger? logger = null)
        : base(services, Dispatcher.CreateDefault(), BaseUri, contentRoot, new(), relativeHostPath)
    {
        _webView = webView;
        _logger = logger;
        HostPageRelativePath = relativeHostPath;

        _ = Dispatcher.InvokeAsync(async () =>
        {
            await AddRootComponentAsync(rootComponentType, "#app", ParameterView.Empty);
        });
    }

    protected override void NavigateCore(Uri absoluteUri)
    {
        _logger?.LogDebug("Navigating to {Uri}", absoluteUri);
        NSApplication.SharedApplication.BeginInvokeOnMainThread(() =>
        {
            _webView.LoadRequest(new NSUrlRequest(new NSUrl(absoluteUri.ToString())));
        });
    }

    protected override void SendMessage(string message)
    {
        var escaped = message
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\n", "\\n")
            .Replace("\r", "\\r");

        var js = $"window.__dispatchMessageCallback(\"{escaped}\")";
        _logger?.LogTrace("Dispatching to JS: {Len} bytes", message.Length);

        NSApplication.SharedApplication.BeginInvokeOnMainThread(() =>
        {
            _webView.EvaluateJavaScript(new NSString(js), null);
        });
    }

    public void ForwardScriptMessage(string message)
    {
        MessageReceived(BaseUri, message);
    }

    internal bool TryServe(string requestUri, out int statusCode, out string statusMessage, out Stream content, out IDictionary<string, string> headers)
        => TryGetResponseContent(requestUri, allowFallbackOnHostPage: false, out statusCode, out statusMessage, out content, out headers);
}