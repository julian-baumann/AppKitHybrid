namespace AppKitHybrid;

using AppKit;
using Foundation;

[Register("AppDelegate")]
public sealed class AppDelegate : NSApplicationDelegate
{
    private MainWindowController? _windowController;

    public override void DidFinishLaunching(NSNotification notification)
    {
        _windowController = new MainWindowController();
        _windowController.ShowWindow(this);
        NSApplication.SharedApplication.ActivateIgnoringOtherApps(true);
    }
}
