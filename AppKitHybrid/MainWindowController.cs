namespace AppKitHybrid;

public sealed class MainWindowController : NSWindowController
{
    private readonly string _url;

    public MainWindowController() : base(new NSWindow(
        new CGRect(0, 0, 1200, 800),
        NSWindowStyle.Titled | NSWindowStyle.Closable | NSWindowStyle.Resizable |
        NSWindowStyle.Miniaturizable,
        NSBackingStore.Buffered,
        deferCreation: false))
    {
        // _url = url;
        Window.Title = "Blazor Hybrid (AppKit)";
        ContentViewController = new WebViewController();
        Window.Center();
    }
}