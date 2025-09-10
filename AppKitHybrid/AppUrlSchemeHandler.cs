namespace AppKitHybrid;

using System.IO;
using Foundation;
using WebKit;

public sealed class AppUrlSchemeHandler : NSObject, IWKUrlSchemeHandler
{
    private readonly string _root;

    public AppUrlSchemeHandler(string rootDirectory)
    {
        _root = rootDirectory;
    }

    public void StartUrlSchemeTask(WKWebView webView, IWKUrlSchemeTask urlSchemeTask)
    {
        var requested = urlSchemeTask.Request?.Url;
        if (requested is null)
        {
            ReplyNotFound(urlSchemeTask, "No URL.");
            return;
        }

        var path = requested.Path ?? "/";
        if (string.IsNullOrWhiteSpace(path) || path == "/")
        {
            path = "/index.html";
        }

        var safePath = Sanitize(path);
        var fullPath = Path.Combine(_root, safePath);

        if (!File.Exists(fullPath) && Directory.Exists(fullPath))
        {
            fullPath = Path.Combine(fullPath, "index.html");
        }

        if (!File.Exists(fullPath))
        {
            ReplyNotFound(urlSchemeTask, $"Not found: {requested.AbsoluteString}");
            return;
        }

        try
        {
            var data = NSData.FromArray(File.ReadAllBytes(fullPath));
            var mime = GetMimeType(fullPath);

            var keys = new NSString[]
            {
                new NSString("Content-Type"),
                new NSString("Cache-Control")
            };
            var values = new NSString[]
            {
                new NSString(mime),
                new NSString("no-cache")
            };

            var headers = new NSDictionary<NSString, NSString>(keys, values);

            var response = new NSHttpUrlResponse(
                requested,
                (nint)200,
                "OK",
                headers
            );

            urlSchemeTask.DidReceiveResponse(response);
            urlSchemeTask.DidReceiveData(data);
            urlSchemeTask.DidFinish();
        }
        catch (System.Exception ex)
        {
            ReplyError(urlSchemeTask, ex);
        }
    }

    public void StopUrlSchemeTask(WKWebView webView, IWKUrlSchemeTask urlSchemeTask)
    {
        // No cleanup required for file-based responses.
    }

    private static string Sanitize(string path)
    {
        var relative = path.Replace('\\', '/');
        if (relative.StartsWith('/'))
        {
            relative = relative[1..];
        }

        relative = relative.Replace("..", string.Empty);
        return relative;
    }

    private static string GetMimeType(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();

        return ext switch
        {
            ".html" or ".htm" => "text/html; charset=utf-8",
            ".js" => "application/javascript",
            ".css" => "text/css",
            ".json" => "application/json",
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".gif" => "image/gif",
            ".svg" => "image/svg+xml",
            ".woff" => "font/woff",
            ".woff2" => "font/woff2",
            ".ttf" => "font/ttf",
            ".ico" => "image/x-icon",
            ".wasm" => "application/wasm",
            _ => "application/octet-stream"
        };
    }

    private static void ReplyNotFound(IWKUrlSchemeTask task, string message)
    {
        var url = task.Request?.Url ?? new NSUrl("app://localhost/404");
        var data = NSData.FromString(message);

        var keys = new NSString[] { new NSString("Content-Type") };
        var values = new NSString[] { new NSString("text/plain; charset=utf-8") };
        var headers = new NSDictionary<NSString, NSString>(keys, values);

        var response = new NSHttpUrlResponse(
            url,
            (nint)404,
            "Not Found",
            headers
        );

        task.DidReceiveResponse(response);
        task.DidReceiveData(data);
        task.DidFinish();
    }

    private static void ReplyError(IWKUrlSchemeTask task, System.Exception ex)
    {
        var url = task.Request?.Url ?? new NSUrl("app://localhost/error");
        var data = NSData.FromString(ex.Message);

        var keys = new NSString[] { new NSString("Content-Type") };
        var values = new NSString[] { new NSString("text/plain; charset=utf-8") };
        var headers = new NSDictionary<NSString, NSString>(keys, values);

        var response = new NSHttpUrlResponse(
            url,
            (nint)500,
            "Internal Error",
            headers
        );

        task.DidReceiveResponse(response);
        task.DidReceiveData(data);
        task.DidFinish();
    }
}
