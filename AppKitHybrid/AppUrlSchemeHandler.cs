namespace AppKitHybrid;

using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Foundation;
using WebKit;

public sealed class AppUrlSchemeHandler : NSObject, IWKUrlSchemeHandler
{
    private readonly string _root;

    public AppUrlSchemeHandler()
    {
        var resources = NSBundle.MainBundle.ResourcePath;
        if (string.IsNullOrWhiteSpace(resources))
        {
            throw new InvalidOperationException("Could not resolve app bundle Resources path.");
        }

        _root = Path.Combine(resources, "wwwroot");
        if (!Directory.Exists(_root))
        {
            throw new DirectoryNotFoundException(_root);
        }
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

        var safeRelative = Sanitize(path);
        var diskPath = Path.GetFullPath(Path.Combine(_root, safeRelative));

        if (!IsUnderRoot(diskPath))
        {
            ReplyNotFound(urlSchemeTask, "Path traversal rejected.");
            return;
        }

        // Directory default document
        if (!File.Exists(diskPath) && Directory.Exists(diskPath))
        {
            var idx = Path.Combine(diskPath, "index.html");
            var idxAlt = Path.Combine(diskPath, "index.htm");
            diskPath = File.Exists(idx) ? idx : File.Exists(idxAlt) ? idxAlt : diskPath;
        }

        try
        {
            if (File.Exists(diskPath))
            {
                SendFile(urlSchemeTask, requested, diskPath);
                return;
            }

            // Fallback: serve embedded assets shipped with the WebView packages
            // e.g., _framework/blazor.webview.js (and friends)
            if (TryGetEmbeddedAsset(safeRelative, out var data, out var mime))
            {
                var headers = new NSDictionary<NSString, NSString>(
                    new NSString[] { new("Content-Type"), new("Cache-Control") },
                    new NSString[] { new(mime), new("no-cache") }
                );

                var response = new NSHttpUrlResponse(requested, (nint)200, "OK", headers);
                urlSchemeTask.DidReceiveResponse(response);
                urlSchemeTask.DidReceiveData(data);
                urlSchemeTask.DidFinish();
                data.Dispose();
                return;
            }

            ReplyNotFound(urlSchemeTask, requested.AbsoluteString ?? "Not found.");
        }
        catch (Exception ex)
        {
            ReplyError(urlSchemeTask, ex);
        }
    }

    public void StopUrlSchemeTask(WKWebView webView, IWKUrlSchemeTask urlSchemeTask)
    {
    }

    private void SendFile(IWKUrlSchemeTask task, NSUrl requested, string fullPath)
    {
        using var stream = File.OpenRead(fullPath);
        using var data = NSData.FromStream(stream);

        var headers = new NSDictionary<NSString, NSString>(
            new NSString[] { new("Content-Type"), new("Cache-Control") },
            new NSString[] { new(GetMimeType(fullPath)), new("no-cache") }
        );

        var response = new NSHttpUrlResponse(requested, (nint)200, "OK", headers);
        task.DidReceiveResponse(response);
        task.DidReceiveData(data);
        task.DidFinish();
    }

    private static string Sanitize(string percentEncodedPath)
    {
        var decoded = Uri.UnescapeDataString(percentEncodedPath);
        var normalized = decoded.Replace('\\', '/');
        if (normalized.StartsWith('/'))
        {
            normalized = normalized[1..];
        }

        while (normalized.Contains("../", StringComparison.Ordinal))
        {
            normalized = normalized.Replace("../", string.Empty, StringComparison.Ordinal);
        }

        return normalized;
    }

    private bool IsUnderRoot(string fullPath)
    {
        var rootFull = Path.GetFullPath(_root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return fullPath.StartsWith(rootFull, StringComparison.Ordinal);
    }

    private static string GetMimeType(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();

        return ext switch
        {
            ".html" or ".htm" => "text/html; charset=utf-8",
            ".js" or ".mjs" or ".cjs" => "application/javascript; charset=utf-8",
            ".css" => "text/css; charset=utf-8",
            ".json" or ".webmanifest" or ".map" => "application/json; charset=utf-8",
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".gif" => "image/gif",
            ".svg" => "image/svg+xml",
            ".webp" => "image/webp",
            ".avif" => "image/avif",
            ".ico" => "image/x-icon",
            ".ttf" => "font/ttf",
            ".otf" => "font/otf",
            ".woff" => "font/woff",
            ".woff2" => "font/woff2",
            ".wasm" => "application/wasm",
            ".txt" => "text/plain; charset=utf-8",
            _ => "application/octet-stream"
        };
    }

    // -------- Embedded asset fallback --------

    private static bool TryGetEmbeddedAsset(string relativePath, out NSData data, out string mime)
    {
        data = default!;
        mime = "application/octet-stream";

        // We only need a fallback for a small set of framework assets.
        // Map the URL path to a resource name suffix to search for.
        var resourceSuffix = relativePath.Replace('/', '.'); // e.g., "_framework/blazor.webview.js" -> "_framework.blazor.webview.js"

        // Scan assemblies that ship the WebView bits.
        var candidateAssemblies = new[]
        {
            typeof(Microsoft.AspNetCore.Components.WebView.WebViewManager).Assembly, // Microsoft.AspNetCore.Components.WebView
            typeof(Microsoft.JSInterop.IJSRuntime).Assembly                          // sometimes helpers are here too
        };

        foreach (var asm in candidateAssemblies.Distinct())
        {
            var names = asm.GetManifestResourceNames();
            var match = names.FirstOrDefault(n =>
                n.EndsWith(resourceSuffix, StringComparison.OrdinalIgnoreCase) ||
                n.EndsWith(Path.GetFileName(relativePath), StringComparison.OrdinalIgnoreCase));

            if (match is null)
            {
                continue;
            }

            using var stream = asm.GetManifestResourceStream(match);
            if (stream is null)
            {
                continue;
            }

            using var ms = new MemoryStream();
            stream.CopyTo(ms);
            data = NSData.FromArray(ms.ToArray());
            mime = GetMimeType(relativePath);
            return true;
        }

        return false;
    }

    private static void ReplyNotFound(IWKUrlSchemeTask task, string message)
    {
        var url = task.Request?.Url ?? new NSUrl("app://localhost/404");
        using var data = NSData.FromString(message);

        var headers = new NSDictionary<NSString, NSString>(
            new NSString[] { new("Content-Type") },
            new NSString[] { new("text/plain; charset=utf-8") }
        );

        var response = new NSHttpUrlResponse(url, (nint)404, "Not Found", headers);
        task.DidReceiveResponse(response);
        task.DidReceiveData(data);
        task.DidFinish();
    }

    private static void ReplyError(IWKUrlSchemeTask task, Exception ex)
    {
        var url = task.Request?.Url ?? new NSUrl("app://localhost/error");
        using var data = NSData.FromString(ex.Message);

        var headers = new NSDictionary<NSString, NSString>(
            new NSString[] { new("Content-Type") },
            new NSString[] { new("text/plain; charset=utf-8") }
        );

        var response = new NSHttpUrlResponse(url, (nint)500, "Internal Error", headers);
        task.DidReceiveResponse(response);
        task.DidReceiveData(data);
        task.DidFinish();
    }
}