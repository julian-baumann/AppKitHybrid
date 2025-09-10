namespace AppKitHybrid;

using AppKit;
using Foundation;

[Register("AppDelegate")]
public sealed class AppDelegate : NSApplicationDelegate
{
    private ServerHost? _server;
    private MainWindowController? _windowController;

    public override async void DidFinishLaunching(NSNotification notification)
    {
        // _server = new ServerHost();
        // var url = await _server.StartAsync();

        _windowController = new MainWindowController();
        _windowController.ShowWindow(this);
        NSApplication.SharedApplication.ActivateIgnoringOtherApps(true);
    }

    public override async void WillTerminate(NSNotification notification)
    {
        if (_server is not null)
        {
            await _server.StopAsync();
        }
    }
}