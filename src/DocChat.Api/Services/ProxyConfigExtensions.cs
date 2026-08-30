using System.Net;
using DocChat.Api.Configuration;

namespace DocChat.Api.Services;

public static class ProxyConfigExtensions
{
    public static HttpClientHandler? TryCreateHttpHandler(this ProxyConfig proxy, ILogger logger)
    {
        if (!proxy.Enabled)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(proxy.Ip) || string.IsNullOrWhiteSpace(proxy.Port))
        {
            logger.LogWarning("Proxy is enabled, but proxy host or port is empty. HttpClient will be created without proxy.");
            return null;
        }

        var webProxy = new WebProxy(new Uri($"http://{proxy.Ip}:{proxy.Port}"));

        if (!string.IsNullOrEmpty(proxy.Login) && !string.IsNullOrEmpty(proxy.Password))
        {
            webProxy.Credentials = new NetworkCredential(proxy.Login, proxy.Password);
        }

        return new HttpClientHandler { Proxy = webProxy, UseProxy = true };
    }
}
