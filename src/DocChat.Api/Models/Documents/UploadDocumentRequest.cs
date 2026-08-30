namespace DocChat.Api.Models.Documents;

public sealed class UploadDocumentRequest
{
    public IFormFile? File { get; init; }

    public string? DocumentId { get; init; }
}
