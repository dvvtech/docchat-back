using DocChat.Api.Configuration;
using Microsoft.Extensions.Options;
using OpenAI;
using OpenAI.Chat;
using System.ClientModel.Primitives;
using System.Net;
using System.Text.Json;

namespace DocChat.Api.Services
{
    public sealed class AiAgentService
    {
        private readonly ILogger<AiAgentService> _logger;
        private readonly SemaphoreSlim _initLock = new(1, 1);

        private ChatClient? _chatClient;        
        private List<ChatTool>? _tools;
        private bool _initialized;

        private readonly AiConfig _aiConfig;
        private readonly ProxyConfig _proxyConfig;

        public AiAgentService(
            IOptions<AiConfig> aiConfig,
            IOptions<ProxyConfig> proxyConfig,
            ILogger<AiAgentService> logger)
        {
            _aiConfig = aiConfig.Value;
            _proxyConfig = proxyConfig.Value;
            _logger = logger;
        }

        private async Task EnsureInitializedAsync(CancellationToken ct)
        {
            if (_initialized) return;

            await _initLock.WaitAsync(ct);
            try
            {
                if (_initialized) return;

                var openAiOptions = new OpenAIClientOptions();
                if (_proxyConfig.Enabled)
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

                var openAi = new OpenAIClient(new System.ClientModel.ApiKeyCredential(_aiConfig.ApiKey), openAiOptions);
                _chatClient = openAi.GetChatClient(_aiConfig.Model);

                _initialized = true;
                _logger.LogInformation("AiAgentService initialized with {ToolCount} tools", _tools?.Count ?? 0);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize AiAgentService");
                throw;
            }
            finally
            {
                _initLock.Release();
            }
        }

        public async Task ProcessQueryAsync(
            List<ChatMessage> history,
            string userMessage,
            CancellationToken ct)
        {
            await EnsureInitializedAsync(ct);

            history.Add(new UserChatMessage(userMessage));

            var options = new ChatCompletionOptions { Temperature = 0.1f };

            ChatCompletion response;
            try
            {
                var completion = await _chatClient!.CompleteChatAsync(history, options, ct);
                response = completion.Value;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "OpenAI API error ");
                return;
            }

            history.Add(new AssistantChatMessage(response));

            if (response.ToolCalls.Count == 0)
            {
                var text = response.Content[0].Text;
                return;
            }
        }
    }
}
