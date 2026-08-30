using DocChat.Api.Models.Documents;

namespace DocChat.Api.Services.Abstractions;

public interface IDocumentIngestionService
{
    Task<DocumentUploadResponse> IngestAsync(IFormFile file, string documentId, CancellationToken ct);
}
