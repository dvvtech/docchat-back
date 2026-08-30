using DocChat.Api.Configuration;
using DocChat.Api.Exceptions;
using DocChat.Api.Services.Abstractions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using SemanticChunkerNET;

namespace DocChat.Api.Services;

public sealed class SemanticDocumentChunker : IDocumentChunker
{
    private readonly SemanticChunker _chunker;

    public SemanticDocumentChunker(
        IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator,
        IOptions<RagConfig> ragConfig)
    {
        _chunker = new SemanticChunker(embeddingGenerator, tokenLimit: ragConfig.Value.SemanticChunkerTokenLimit);
    }

    public async Task<IReadOnlyList<EmbeddedChunk>> ChunkAsync(string text, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return Array.Empty<EmbeddedChunk>();
        }

        IList<Chunk> chunks;
        try
        {
            chunks = await _chunker.CreateChunksAsync(text, ct);
        }
        catch (Exception ex)
        {
            throw new DocumentProcessingException("Failed to split document into semantic chunks.", ex);
        }

        return chunks
            .Where(chunk => !string.IsNullOrWhiteSpace(chunk.Text))
            .Select(chunk => new EmbeddedChunk(chunk.Text, chunk.Embedding.Vector.ToArray()))
            .ToArray();
    }
}
