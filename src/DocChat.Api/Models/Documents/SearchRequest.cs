namespace DocChat.Api.Models.Documents;

public sealed record SearchRequest(
    string Query,
    int TopK);
