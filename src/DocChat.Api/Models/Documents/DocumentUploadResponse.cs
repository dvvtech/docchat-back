namespace DocChat.Api.Models.Documents
{
    public sealed record DocumentUploadResponse(
        string DocumentId,
        string FileName,
        int TextCharacterCount,
        int ChunkCount,
        IReadOnlyCollection<DocumentChunkDto> Chunks);
}
