using System.Net;
using DocChat.Api.Configuration;

namespace DocChat.Api.Services;

public static class ProxyConfigExtensions
{
    public static HttpClientHandler? TryCreateHttpHandler(this ProxyConfig proxyConfig, ILogger logger)
    {        
        if (string.IsNullOrWhiteSpace(proxyConfig.Url))
        {
            logger.LogWarning("Proxy is enabled, but proxy host or port is empty. HttpClient will be created without proxy.");
            return null;
        }

        var webProxy = new WebProxy
        {
            Address = new Uri(proxyConfig.Url),
            BypassProxyOnLocal = false,
            UseDefaultCredentials = false,
            Credentials = (!string.IsNullOrEmpty(proxyConfig.Login) &&
                                       !string.IsNullOrEmpty(proxyConfig.Password))
                            ? new NetworkCredential(proxyConfig.Login, proxyConfig.Password)
                            : null
        };

        return new HttpClientHandler { Proxy = webProxy, UseProxy = true };
    }
}
