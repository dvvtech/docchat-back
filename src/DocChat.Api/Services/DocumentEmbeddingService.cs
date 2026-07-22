using DocChat.Api.Configuration;
using Microsoft.Extensions.Options;
using OpenAI.Embeddings;

namespace DocChat.Api.Services
{
    public sealed class DocumentEmbeddingService
    {
        private readonly RagConfig _ragConfig;
        private readonly OpenAiClientFactory _openAiClientFactory;
        private readonly SemaphoreSlim _initLock = new(1, 1);

        private EmbeddingClient? _embeddingClient;
        private bool _initialized;

        public DocumentEmbeddingService(
            IOptions<RagConfig> ragConfig,
            OpenAiClientFactory openAiClientFactory)
        {
            _ragConfig = ragConfig.Value;
            _openAiClientFactory = openAiClientFactory;
        }

        public async Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken ct)
        {
            await EnsureInitializedAsync(ct);

            var embedding = await _embeddingClient!.GenerateEmbeddingAsync(text, cancellationToken: ct);
            return embedding.Value.ToFloats().ToArray();
        }

        private async Task EnsureInitializedAsync(CancellationToken ct)
        {
            if (_initialized) return;

            await _initLock.WaitAsync(ct);
            try
            {
                if (_initialized) return;

                var openAi = _openAiClientFactory.CreateClient();
                _embeddingClient = openAi.GetEmbeddingClient(_ragConfig.EmbeddingModel);
                _initialized = true;
            }
            finally
            {
                _initLock.Release();
            }
        }
    }
}
