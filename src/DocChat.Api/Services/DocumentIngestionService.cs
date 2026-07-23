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
            var text = await _textExtractor.ExtractTextAsync(file, ct);
            if (string.IsNullOrWhiteSpace(text))
            {
                throw new InvalidOperationException("Document does not contain extractable text.");
            }

            var chunks = await _chunker.ChunkAsync(text, ct);
            if (chunks.Count == 0)
            {
                throw new InvalidOperationException("Document was not split into chunks.");
            }

            var embeddings = new List<float[]>(chunks.Count);
            foreach (var chunk in chunks)
            {
                embeddings.Add(await _embeddingService.GenerateEmbeddingAsync(chunk, ct));
            }

            await _documentStore.SaveChunksAsync(documentId, file.FileName, chunks, embeddings, ct);

            return new DocumentUploadResponse(
                documentId,
                file.FileName,
                text.Length,
                chunks.Count,
                chunks.Select((chunk, index) => new DocumentChunkDto(
                    index,
                    chunk.Length,
                    chunk.Length <= 160 ? chunk : chunk[..160]))
                .ToArray());
        }
    }
}
