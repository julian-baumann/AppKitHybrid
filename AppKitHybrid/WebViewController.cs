using AppKit;
using CoreGraphics;
using Foundation;
using WebKit;

namespace AppKitHybrid;

public sealed class WebViewController : NSViewController, IWKNavigationDelegate
{
    private readonly Uri _baseUri;

    public WebViewController(string baseUrl)
    {
        _baseUri = new Uri(baseUrl, UriKind.Absolute);
    }

    public override void LoadView()
    {
        View = new NSView(new CGRect(0, 0, 1200, 800));
    }

    public override void ViewDidLoad()
    {
        base.ViewDidLoad();

        var config = new WKWebViewConfiguration();
        var webView = new WKWebView(View.Bounds, config)
        {
            AutoresizingMask = NSViewResizingMask.WidthSizable | NSViewResizingMask.HeightSizable,
            NavigationDelegate = this,
            Inspectable = true
        };

        View.AddSubview(webView);
        webView.LoadRequest(new NSUrlRequest(new NSUrl(_baseUri + "/index.html")));
    }

    [Export("webView:decidePolicyForNavigationAction:decisionHandler:")]
    public void DecidePolicy(WKWebView webView, WKNavigationAction action, System.Action<WKNavigationActionPolicy> decisionHandler)
    {
        var url = action.Request?.Url;
        if (url is null)
        {
            decisionHandler(WKNavigationActionPolicy.Allow);
            return;
        }

        if (IsSameOriginLoopback(url))
        {
            decisionHandler(WKNavigationActionPolicy.Allow);
            return;
        }

        if (url.Scheme is "http" or "https")
        {
            NSWorkspace.SharedWorkspace.OpenUrl(url);
            decisionHandler(WKNavigationActionPolicy.Cancel);
            return;
        }

        decisionHandler(WKNavigationActionPolicy.Allow);
    }

    private bool IsSameOriginLoopback(NSUrl url)
    {
        if (url.Scheme is not ("http" or "https"))
        {
            return false;
        }

        var host = url.Host?.ToLowerInvariant();
        var isLoopback = host is "127.0.0.1" or "localhost";
        if (!isLoopback)
        {
            return false;
        }

        var port = url.Port;
        return port == _baseUri.Port;
    }
}