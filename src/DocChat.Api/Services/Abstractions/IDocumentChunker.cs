namespace DocChat.Api.Services.Abstractions;

public sealed record EmbeddedChunk(
    string Text,
    float[] Vector);

public interface IDocumentChunker
{
    Task<IReadOnlyList<EmbeddedChunk>> ChunkAsync(string text, CancellationToken ct);
}
