using DocChat.Api.Services.Abstractions;
using Microsoft.Extensions.AI;

namespace DocChat.Api.Services;

public sealed class DocumentEmbeddingService : IEmbeddingService
{
    private readonly IEmbeddingGenerator<string, Embedding<float>> _embeddingGenerator;

    public DocumentEmbeddingService(IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator)
    {
        _embeddingGenerator = embeddingGenerator;
    }

    public async Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken ct)
    {
        var embeddings = await _embeddingGenerator.GenerateAsync([text], cancellationToken: ct);
        return embeddings.First().Vector.ToArray();
    }
}
