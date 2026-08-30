using DocChat.Api.Exceptions;
using DocChat.Api.Models.Documents;
using DocChat.Api.Services.Abstractions;

namespace DocChat.Api.Services;

public sealed class DocumentIngestionService : IDocumentIngestionService
{
    private readonly IDocumentTextExtractor _textExtractor;
    private readonly IDocumentChunker _chunker;
    private readonly IDocumentVectorStore _documentStore;

    public DocumentIngestionService(
        IDocumentTextExtractor textExtractor,
        IDocumentChunker chunker,
        IDocumentVectorStore documentStore)
    {
        _textExtractor = textExtractor;
        _chunker = chunker;
        _documentStore = documentStore;
    }

    public async Task<DocumentUploadResponse> IngestAsync(IFormFile file, string documentId, CancellationToken ct)
    {
        if (file.Length == 0)
        {
            throw new ValidationException("Uploaded file is empty.");
        }

        if (string.IsNullOrWhiteSpace(documentId))
        {
            throw new ValidationException("Document ID is required.");
        }

        var totalChars = 0;
        var chunkIndexOffset = 0;
        var allChunks = new List<DocumentChunkDto>();

        await _documentStore.DeleteDocumentAsync(documentId, ct);

        await foreach (var textPage in _textExtractor.ExtractTextPagesAsync(file, ct))
        {
            ct.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(textPage))
                continue;

            totalChars += textPage.Length;

            var chunks = await _chunker.ChunkAsync(textPage, ct);
            if (chunks.Count == 0)
                continue;

            await _documentStore.SaveChunksAsync(documentId, file.FileName, chunks, ct, chunkIndexOffset);

            for (var i = 0; i < chunks.Count; i++)
            {
                var chunkText = chunks[i].Text;
                allChunks.Add(new DocumentChunkDto(
                    chunkIndexOffset + i,
                    chunkText.Length,
                    chunkText.Length <= 160 ? chunkText : chunkText[..160]));
            }

            chunkIndexOffset += chunks.Count;
        }

        if (totalChars == 0)
        {
            throw new DocumentProcessingException("Document does not contain extractable text.");
        }

        return new DocumentUploadResponse(
            documentId,
            file.FileName,
            totalChars,
            allChunks.Count,
            allChunks);
    }
}
