using DocChat.Api.Models.Documents;

namespace DocChat.Api.Services.Abstractions;

public interface IDocumentSearchService
{
    Task<SearchResponse> SearchAsync(SearchRequest request, CancellationToken ct);
}
