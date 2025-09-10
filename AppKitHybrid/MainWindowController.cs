namespace AppKitHybrid;

using CoreGraphics;
using AppKit;

public sealed class MainWindowController : NSWindowController
{
    public MainWindowController() : base(new NSWindow(
        new CGRect(0, 0, 1200, 800),
        NSWindowStyle.Titled | NSWindowStyle.Closable | NSWindowStyle.Resizable | NSWindowStyle.Miniaturizable,
        NSBackingStore.Buffered,
        deferCreation: false))
    {
        Window.Title = "Blazor Hybrid (In-Process)";
        ContentViewController = new WebViewController();
        Window.Center();
    }
}
