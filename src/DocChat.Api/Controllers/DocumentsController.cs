using DocChat.Api.Models.Documents;
using DocChat.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace DocChat.Api.Controllers
{
    [ApiController]
    [Route("documents")]
    public sealed class DocumentsController : ControllerBase
    {
    private readonly DocumentIngestionService _documentIngestionService;
    private readonly DocumentSearchService _documentSearchService;
    private readonly QdrantDocumentStore _documentStore;

    public DocumentsController(
        DocumentIngestionService documentIngestionService,
        DocumentSearchService documentSearchService,
        QdrantDocumentStore documentStore)
    {
        _documentIngestionService = documentIngestionService;
        _documentSearchService = documentSearchService;
        _documentStore = documentStore;
    }

        [HttpPost("upload")]
        [Consumes("multipart/form-data")]
        [ProducesResponseType(typeof(DocumentUploadResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<DocumentUploadResponse>> Upload(
            [FromForm] IFormFile? file,
            [FromForm] string? documentId,
            CancellationToken cancellationToken)
        {
            if (file is null)
            {
                return BadRequest(new { error = "File is required." });
            }

            if (string.IsNullOrWhiteSpace(documentId))
            {
                return BadRequest(new { error = "Document ID is required." });
            }

            try
            {
                var response = await _documentIngestionService.IngestAsync(file, documentId, cancellationToken);
                return Ok(response);
            }
            catch (NotSupportedException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpPost("search")]
        [ProducesResponseType(typeof(SearchResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<SearchResponse>> Search(
            [FromBody] SearchRequest request,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request?.Query))
            {
                return BadRequest(new { error = "Query is required." });
            }

            try
            {
                var response = await _documentSearchService.SearchAsync(request, cancellationToken);
                return Ok(response);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpDelete("{documentId}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult> Delete(
            string documentId,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(documentId))
            {
                return BadRequest(new { error = "Document ID is required." });
            }

            try
            {
                await _documentStore.DeleteDocumentAsync(documentId, cancellationToken);
                return Ok(new { deleted = true });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }
    }
}
