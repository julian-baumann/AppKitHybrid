using System.IO;
using AppKit;
using CoreGraphics;
using Foundation;
using Microsoft.AspNetCore.Components.WebView;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using WebKit;

namespace AppKitHybrid;

public sealed class WebViewController : NSViewController
{
    private WKWebView? _webView;
    private WebViewManager? _manager;

    public override void LoadView()
    {
        View = new NSView(new CGRect(0, 0, 1200, 800));
    }

    public override void ViewDidLoad()
    {
        base.ViewDidLoad();

        var resources = NSBundle.MainBundle.ResourcePath!;
        var wwwroot = Path.Combine(resources, "wwwroot");

        // Services used by WebViewManager (matches WinForms/WPF samples)
        var services = new ServiceCollection()
            .AddLogging()
            .AddBlazorWebView()      // registers JSRuntime, dispatcher, file provider helpers, etc.
            .BuildServiceProvider();

        var config = new WKWebViewConfiguration();
        var ucc = new WKUserContentController();

        // Define window.external bridge at document start
        var bridgeJs = """
            (() => {
                const h = 'webwindowinterop';
                const w = window;
                if (!w.external) { w.external = {}; }
                if (!w.external.sendMessage) {
                    w.external.sendMessage = (m) => {
                        window.webkit?.messageHandlers?.[h]?.postMessage(m);
                    };
                }
                if (!w.external.receiveMessage) {
                    w.external.receiveMessage = (_) => {};
                }
            })();
            """;
        ucc.AddUserScript(new WKUserScript(new NSString(bridgeJs), WKUserScriptInjectionTime.AtDocumentStart, true));
        ucc.AddScriptMessageHandler(new ScriptMessageHandler(msg => _manager?.MessageReceived(msg)), "webwindowinterop");

        config.UserContentController = ucc;
        config.SetUrlSchemeHandler(new AppUrlSchemeHandler(), "app");

        _webView = new WKWebView(View.Bounds, config)
        {
            AutoresizingMask = NSViewResizingMask.WidthSizable | NSViewResizingMask.HeightSizable,
            Inspectable = true
        };

        View.AddSubview(_webView);

        // Create the WebViewManager with a PhysicalFileProvider pointing at our bundle’s wwwroot
        var fileProvider = new PhysicalFileProvider(wwwroot);

        _manager = new WebViewManager(
            services,
            fileProvider,
            rootComponents: new()
            {
                new RootComponent("app", typeof(BlazorApp.Components.App)) // #app in your index.html
            },
            jsRootComponentParametersById: new(),
            scheme: "app",
            hostPageRelativePath: "index.html",
            startAddress: new Uri("app://localhost/"));

        // Hook the native <-> JS adapters
        _manager.AttachWebView(
            postMessage: message =>
            {
                if (_webView is null)
                {
                    return;
                }

                var js = $"window.external.receiveMessage({EscapeForJs(message)});";
                _webView.EvaluateJavaScript(new NSString(js), null);
            },
            navigate: uri =>
            {
                if (_webView is null)
                {
                    return;
                }

                _webView.LoadRequest(new NSUrlRequest(new NSUrl(uri)));
            });

        // Navigate to start the app
        _manager.Navigate("/");
    }

    private static string EscapeForJs(string s) =>
        "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r") + "\"";

    private sealed class ScriptMessageHandler : NSObject, IWKScriptMessageHandler
    {
        private readonly Action<string> _onMessage;
        public ScriptMessageHandler(Action<string> onMessage) => _onMessage = onMessage;
        public void DidReceiveScriptMessage(WKUserContentController _, WKScriptMessage msg)
        {
            _onMessage(msg.Body?.ToString() ?? string.Empty);
        }
    }
}