namespace DocChat.Api.Services.Abstractions;

public interface IDocumentTextExtractor
{
    IAsyncEnumerable<string> ExtractTextPagesAsync(IFormFile file, CancellationToken ct);
}
