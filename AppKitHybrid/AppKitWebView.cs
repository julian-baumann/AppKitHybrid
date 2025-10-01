using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;
using WebKit;

namespace AppKitHybrid;

public sealed class BlazorWebView : NSViewController
{
    private WKWebView? _webView;
    private AppKitWebViewManager? _manager;

    public override void LoadView()
    {
        View = new NSView(new CoreGraphics.CGRect(0, 0, 1200, 800));
    }

    public override void ViewDidLoad()
    {
        base.ViewDidLoad();

        var services = Program.ServiceProvider;
        var options = services.GetRequiredService<BlazorWebViewOptions>();

        var config = new WKWebViewConfiguration();
        var ucc = new WKUserContentController();

        var bridgeJs = """
            (() => {
                const h = 'webview';
                if (!window.external) { window.external = {}; }
                if (!window.__receiveMessageCallbacks) { window.__receiveMessageCallbacks = []; }
                if (!window.external.sendMessage) { window.external.sendMessage = m => window.webkit?.messageHandlers?.[h]?.postMessage(m); }
                if (!window.external.receiveMessage) { window.external.receiveMessage = cb => window.__receiveMessageCallbacks.push(cb); }
                window.__dispatchMessageCallback = m => { for (const cb of window.__receiveMessageCallbacks) { try { cb(m); } catch { } } };
            })();
            """;

        ucc.AddUserScript(new WKUserScript(new NSString(bridgeJs), WKUserScriptInjectionTime.AtDocumentStart, true));
        ucc.AddScriptMessageHandler(new ScriptMessageHandler(msg => _manager?.ForwardScriptMessage(msg)), "webview");

        config.UserContentController = ucc;
        config.SetUrlSchemeHandler(new AppUrlSchemeHandlerWithManager(() => _manager!), "app");
        
        // self.webView!.isOpaque = false
        // self.webView!.backgroundColor = UIColor.clear
        // self.webView!.scrollView.backgroundColor = UIColor.clear
        _webView = new WKWebView(View.Bounds, config)
        {
            AutoresizingMask = NSViewResizingMask.WidthSizable | NSViewResizingMask.HeightSizable,
            Inspectable = true,
            UnderPageBackgroundColor = NSColor.Clear
        };
        _webView.SetValueForKey(NSObject.FromObject(false), new NSString("drawsBackground"));
        
        View.AddSubview(_webView);

        _manager = new AppKitWebViewManager(
            _webView,
            services,
            new PhysicalFileProvider(Path.Combine(NSBundle.MainBundle.ResourcePath!, "wwwroot")),
            options.RelativeHostPath,
            options.RootComponent,
            services.GetService<ILogger<AppKitWebViewManager>>());

        _manager.Navigate(new Uri(AppKitWebViewManager.BaseUri, "/").ToString());
    }

    private sealed class ScriptMessageHandler : NSObject, IWKScriptMessageHandler
    {
        private readonly Action<string> _onMessage;
        public ScriptMessageHandler(Action<string> onMessage) { _onMessage = onMessage; }
        public void DidReceiveScriptMessage(WKUserContentController _, WKScriptMessage message)
        {
            _onMessage(message.Body?.ToString() ?? string.Empty);
        }
    }
}