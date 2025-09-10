using WebKit;

namespace AppKitHybrid;

public sealed class WebViewController : NSViewController, IWKNavigationDelegate
{
    private WKWebView? _webView;

    public override void LoadView()
    {
        View = new NSView(new CGRect(0, 0, 1200, 800));
    }

    public override void ViewDidLoad()
    {
        base.ViewDidLoad();

        var config = new WKWebViewConfiguration
        {
            Preferences = new WKPreferences
            {
                JavaScriptEnabled = true,
                JavaScriptCanOpenWindowsAutomatically = true
            }
        };

        _webView = new BlazorWebView(Program.ServiceProvider)
        {
            Frame = View.Bounds,
            AutoresizingMask = NSViewResizingMask.WidthSizable | NSViewResizingMask.HeightSizable
        };

        // _webView = new WKWebView(View.Bounds, config)
        // {
        //     AutoresizingMask = NSViewResizingMask.WidthSizable | NSViewResizingMask.HeightSizable,
        //     NavigationDelegate = this
        // };

        View.AddSubview(_webView);
        // _webView.LoadRequest(new NSUrlRequest(new NSUrl(url)));
    }
}