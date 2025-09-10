namespace AppKitHybrid;

using CoreGraphics;
using Foundation;
using WebKit;
using AppKit;

public sealed class WebViewController : NSViewController, IWKNavigationDelegate
{
    private WKWebView? _webView;

    public WebViewController()
    {
    }

    public override void LoadView()
    {
        View = new NSView(new CGRect(0, 0, 1200, 800));
    }

    public override void ViewDidLoad()
    {
        base.ViewDidLoad();

        var bundleRoot = NSBundle.MainBundle.ResourcePath ?? string.Empty;
        var wwwroot = System.IO.Path.Combine(bundleRoot, "wwwroot");

        var config = new WKWebViewConfiguration();
        config.SetUrlSchemeHandler(new AppUrlSchemeHandler(wwwroot), "app");

        _webView = new WKWebView(View.Bounds, config)
        {
            AutoresizingMask = NSViewResizingMask.WidthSizable | NSViewResizingMask.HeightSizable,
            NavigationDelegate = this,
            Inspectable = true
        };

        View.AddSubview(_webView);

        // Load the app's entry point in-process via the custom scheme.
        var startUrl = new NSUrl("app://localhost");
        _webView.LoadRequest(new NSUrlRequest(startUrl));
    }

    [Export("webView:decidePolicyForNavigationAction:decisionHandler:")]
    public void DecidePolicy(WKWebView webView, WKNavigationAction navigationAction, System.Action<WKNavigationActionPolicy> decisionHandler)
    {
        var url = navigationAction.Request?.Url;
        if (url is not null && (url.Scheme == "http" || url.Scheme == "https"))
        {
            NSWorkspace.SharedWorkspace.OpenUrl(url);
            decisionHandler(WKNavigationActionPolicy.Cancel);
            return;
        }

        decisionHandler(WKNavigationActionPolicy.Allow);
    }
}
