using DocChat.Api.Models.Documents;
using DocChat.Api.Services.Abstractions;

namespace DocChat.Api.Services;

public sealed class NoOpDocumentReranker : IDocumentReranker
{
    public Task<IReadOnlyList<SearchResult>> RerankAsync(
        string query,
        IReadOnlyList<SearchResult> candidates,
        CancellationToken ct)
    {
        IReadOnlyList<SearchResult> result = candidates
            .OrderByDescending(candidate => candidate.Score)
            .ToArray();

        return Task.FromResult(result);
    }
}
