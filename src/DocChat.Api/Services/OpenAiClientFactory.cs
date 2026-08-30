using DocChat.Api.Configuration;
using Microsoft.Extensions.Options;
using OpenAI;
using System.ClientModel;
using System.ClientModel.Primitives;

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
            var handler = _proxyConfig.TryCreateHttpHandler(_logger);

            if (handler is not null)
            {
                openAiOptions.Transport = new HttpClientPipelineTransport(new HttpClient(handler));
                _logger.LogInformation("OpenAI client configured with proxy {ProxyIp}:{ProxyPort}", _proxyConfig.Ip, _proxyConfig.Port);
            }

            return new OpenAIClient(new ApiKeyCredential(_aiConfig.ApiKey), openAiOptions);
        }
    }
}
