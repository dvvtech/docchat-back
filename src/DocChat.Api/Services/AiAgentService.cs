using DocChat.Api.Configuration;
using Microsoft.Extensions.Options;
using OpenAI.Chat;

namespace DocChat.Api.Services
{
    public sealed class AiAgentService
    {
        private readonly ILogger<AiAgentService> _logger;
        private readonly SemaphoreSlim _initLock = new(1, 1);

        private ChatClient? _chatClient;
        private bool _initialized;

        private readonly AiConfig _aiConfig;
        private readonly OpenAiClientFactory _openAiClientFactory;

        public AiAgentService(
            IOptions<AiConfig> aiConfig,
            OpenAiClientFactory openAiClientFactory,
            ILogger<AiAgentService> logger)
        {
            _aiConfig = aiConfig.Value;
            _openAiClientFactory = openAiClientFactory;
            _logger = logger;
        }

        private async Task EnsureInitializedAsync(CancellationToken ct)
        {
            if (_initialized) return;

            await _initLock.WaitAsync(ct);
            try
            {
                if (_initialized) return;

                var openAi = _openAiClientFactory.CreateClient();
                _chatClient = openAi.GetChatClient(_aiConfig.Model);

                _initialized = true;
                _logger.LogInformation("AiAgentService initialized");
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
