namespace DocChat.Api.Services.Abstractions;

public interface IEmbeddingService
{
    Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken ct);
}
