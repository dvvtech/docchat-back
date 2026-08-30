using DocChat.Api.Models.Documents;

namespace DocChat.Api.Services.Abstractions;

public interface IDocumentVectorStore
{
    Task SaveChunksAsync(
        string documentId,
        string fileName,
        IReadOnlyList<EmbeddedChunk> chunks,
        CancellationToken ct,
        int chunkIndexOffset = 0);

    Task DeleteDocumentAsync(string documentId, CancellationToken ct);

    Task<IReadOnlyList<SearchResult>> SearchAsync(
        float[] queryVector,
        int limit,
        CancellationToken ct);
}
