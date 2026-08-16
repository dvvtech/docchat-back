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

        public async Task<IReadOnlyList<float[]>> GenerateEmbeddingsAsync(
            IReadOnlyList<string> texts,
            CancellationToken ct)
        {
            if (texts.Count == 0)
            {
                return Array.Empty<float[]>();
            }

            await EnsureInitializedAsync(ct);

            var embeddings = new List<float[]>(texts.Count);
            foreach (var batch in texts.Chunk(Math.Max(1, _ragConfig.EmbeddingBatchSize)))
            {
                ct.ThrowIfCancellationRequested();

                var response = await _embeddingClient!.GenerateEmbeddingsAsync(batch, cancellationToken: ct);
                embeddings.AddRange(response.Value.Select(embedding => embedding.ToFloats().ToArray()));
            }

            return embeddings;
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
