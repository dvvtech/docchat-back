namespace DocChat.Api.Models.Documents;

public sealed record SearchResult(
    string DocumentId,
    string FileName,
    int ChunkIndex,
    string Text,
    double Score);
