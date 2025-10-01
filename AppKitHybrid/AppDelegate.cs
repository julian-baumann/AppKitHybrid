namespace AppKitHybrid;

using AppKit;
using Foundation;

[Register("AppDelegate")]
public sealed class AppDelegate : NSApplicationDelegate
{
    public override void DidFinishLaunching(NSNotification notification)
    {
        var window = new NSWindow(
            new CGRect(0, 0, 1200, 800),
            NSWindowStyle.Titled | NSWindowStyle.Closable | NSWindowStyle.Resizable,
            NSBackingStore.Buffered,
            deferCreation: false);

        window.Title = "AppKitHybrid";
        window.Center();
        window.ContentViewController = new BlazorWebView();
        window.MakeKeyAndOrderFront(null);
    }
}
