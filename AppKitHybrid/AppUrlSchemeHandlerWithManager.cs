using AppKitHybrid;
using WebKit;

public sealed class AppUrlSchemeHandlerWithManager : NSObject, IWKUrlSchemeHandler
{
    private readonly Func<AppKitWebViewManager> _manager;
    public AppUrlSchemeHandlerWithManager(Func<AppKitWebViewManager> manager) { _manager = manager; }

    public void StartUrlSchemeTask(WKWebView webView, IWKUrlSchemeTask task)
    {
        var request = task.Request?.Url;
        if (request is null)
        {
            FinishText(task, 400, "Bad Request", "Missing URL");
            return;
        }

        var uri = request.ToString();
        if (request.Path is "/" or null)
        {
            var rel = _manager().HostPageRelativePath;
            uri = new Uri(new Uri(uri), rel).ToString();
        }

        if (_manager().TryServe(uri, out var status, out var message, out var content, out var headers))
        {
            try
            {
                var nsHeaders = new NSMutableDictionary<NSString, NSString>();
                foreach (var kvp in headers)
                {
                    nsHeaders.SetValueForKey(new NSString(kvp.Value), new NSString(kvp.Key));
                }

                var response = new NSHttpUrlResponse(request, (nint)status, message, nsHeaders);
                task.DidReceiveResponse(response);

                using var ms = new MemoryStream();
                content.CopyTo(ms);
                using var data = NSData.FromArray(ms.ToArray());
                task.DidReceiveData(data);
                task.DidFinish();
            }
            finally
            {
                content.Dispose();
            }
        }
        else
        {
            FinishText(task, status, message, $"{status} {message}");
        }
    }

    public void StopUrlSchemeTask(WKWebView webView, IWKUrlSchemeTask urlSchemeTask) { }

    private static void FinishText(IWKUrlSchemeTask task, int code, string reason, string text)
    {
        var headers = new NSDictionary<NSString, NSString>(new NSString("Content-Type"), new NSString("text/plain; charset=utf-8"));
        var resp = new NSHttpUrlResponse(task.Request?.Url ?? new NSUrl("app://localhost/"), (nint)code, reason, headers);
        task.DidReceiveResponse(resp);
        using var data = NSData.FromString(text);
        task.DidReceiveData(data);
        task.DidFinish();
    }
}