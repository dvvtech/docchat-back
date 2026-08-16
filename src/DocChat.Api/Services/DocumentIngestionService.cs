using DocChat.Api.Models.Documents;

namespace DocChat.Api.Services
{
    public sealed class DocumentIngestionService
    {
        private readonly DocumentTextExtractor _textExtractor;
        private readonly LlmDocumentChunker _chunker;
        private readonly DocumentEmbeddingService _embeddingService;
        private readonly QdrantDocumentStore _documentStore;

        public DocumentIngestionService(
            DocumentTextExtractor textExtractor,
            LlmDocumentChunker chunker,
            DocumentEmbeddingService embeddingService,
            QdrantDocumentStore documentStore)
        {
            _textExtractor = textExtractor;
            _chunker = chunker;
            _embeddingService = embeddingService;
            _documentStore = documentStore;
        }

        public async Task<DocumentUploadResponse> IngestAsync(IFormFile file, string documentId, CancellationToken ct)
        {
            if (file.Length == 0)
            {
                throw new InvalidOperationException("Uploaded file is empty.");
            }

            if (string.IsNullOrWhiteSpace(documentId))
            {
                throw new InvalidOperationException("Document ID is required.");
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

                var embeddings = await _embeddingService.GenerateEmbeddingsAsync(chunks, ct);

                await _documentStore.SaveChunksAsync(
                    documentId, file.FileName, chunks, embeddings, ct, chunkIndexOffset);

                for (var i = 0; i < chunks.Count; i++)
                {
                    allChunks.Add(new DocumentChunkDto(
                        chunkIndexOffset + i,
                        chunks[i].Length,
                        chunks[i].Length <= 160 ? chunks[i] : chunks[i][..160]));
                }

                chunkIndexOffset += chunks.Count;
            }

            if (totalChars == 0)
            {
                throw new InvalidOperationException("Document does not contain extractable text.");
            }

            return new DocumentUploadResponse(
                documentId,
                file.FileName,
                totalChars,
                allChunks.Count,
                allChunks);
        }
    }
}
