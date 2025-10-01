using Microsoft.Extensions.DependencyInjection;

namespace AppKitHybrid;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddBlazorWebViewOptions(this IServiceCollection services, BlazorWebViewOptions options)
    {
        return services
            .AddBlazorWebView()
            .AddSingleton(options);
    }
}