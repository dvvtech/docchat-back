namespace DocChat.Api.Models.Documents;

public sealed record SearchResultDto(
    string DocumentId,
    string FileName,
    int ChunkIndex,
    string Text,
    double Score);
