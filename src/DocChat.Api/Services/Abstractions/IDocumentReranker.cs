using DocChat.Api.Models.Documents;

namespace DocChat.Api.Services.Abstractions;

public interface IDocumentReranker
{
    Task<IReadOnlyList<SearchResult>> RerankAsync(
        string query,
        IReadOnlyList<SearchResult> candidates,
        CancellationToken ct);
}
