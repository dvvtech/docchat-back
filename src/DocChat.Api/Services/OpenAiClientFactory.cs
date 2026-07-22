using DocChat.Api.Configuration;
using Microsoft.Extensions.Options;
using OpenAI;
using System.ClientModel;
using System.ClientModel.Primitives;
using System.Net;

namespace DocChat.Api.Services
{
    public sealed class OpenAiClientFactory
    {
        private readonly AiConfig _aiConfig;
        private readonly ProxyConfig _proxyConfig;
        private readonly ILogger<OpenAiClientFactory> _logger;

        public OpenAiClientFactory(
            IOptions<AiConfig> aiConfig,
            IOptions<ProxyConfig> proxyConfig,
            ILogger<OpenAiClientFactory> logger)
        {
            _aiConfig = aiConfig.Value;
            _proxyConfig = proxyConfig.Value;
            _logger = logger;
        }

        public OpenAIClient CreateClient()
        {
            var openAiOptions = new OpenAIClientOptions();
            if (_proxyConfig.Enabled && !string.IsNullOrWhiteSpace(_proxyConfig.Ip) && !string.IsNullOrWhiteSpace(_proxyConfig.Port))
            {
                var proxyUri = new Uri($"http://{_proxyConfig.Ip}:{_proxyConfig.Port}");
                var proxy = new WebProxy(proxyUri);

                if (!string.IsNullOrEmpty(_proxyConfig.Login) && !string.IsNullOrEmpty(_proxyConfig.Password))
                {
                    proxy.Credentials = new NetworkCredential(_proxyConfig.Login, _proxyConfig.Password);
                }

                var handler = new HttpClientHandler
                {
                    Proxy = proxy,
                    UseProxy = true,
                };

                openAiOptions.Transport = new HttpClientPipelineTransport(new HttpClient(handler));
                _logger.LogInformation("OpenAI client configured with proxy {ProxyIp}:{ProxyPort}", _proxyConfig.Ip, _proxyConfig.Port);
            }
            else if (_proxyConfig.Enabled)
            {
                _logger.LogWarning("Proxy is enabled, but proxy host or port is empty. OpenAI client will be created without proxy.");
            }

            return new OpenAIClient(new ApiKeyCredential(_aiConfig.ApiKey), openAiOptions);
        }
    }
}
