namespace DocChat.Api.Models.Documents;

public sealed record SearchResponse(
    string Answer,
    IReadOnlyCollection<SearchResultDto> Sources);
