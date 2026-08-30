using DocChat.Api.Exceptions;
using DocChat.Api.Models;
using DocChat.Api.Models.Documents;
using DocChat.Api.Services.Abstractions;
using Microsoft.AspNetCore.Mvc;

namespace DocChat.Api.Controllers
{
    [ApiController]
    [Route("documents")]
    public sealed class DocumentsController : ControllerBase
    {
        private readonly IDocumentIngestionService _documentIngestionService;
        private readonly IDocumentSearchService _documentSearchService;
        private readonly IDocumentVectorStore _documentStore;

        public DocumentsController(
            IDocumentIngestionService documentIngestionService,
            IDocumentSearchService documentSearchService,
            IDocumentVectorStore documentStore)
        {
            _documentIngestionService = documentIngestionService;
            _documentSearchService = documentSearchService;
            _documentStore = documentStore;
        }

        [HttpPost("upload")]
        [Consumes("multipart/form-data")]
        [ProducesResponseType(typeof(DocumentUploadResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status415UnsupportedMediaType)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status422UnprocessableEntity)]
        public async Task<ActionResult<DocumentUploadResponse>> Upload(
            [FromForm] UploadDocumentRequest? request,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request?.DocumentId))
            {
                throw new ValidationException("Document ID is required.");
            }

            if (request.File is null || request.File.Length == 0)
            {
                throw new ValidationException("File is required.");
            }

            var response = await _documentIngestionService.IngestAsync(request.File, request.DocumentId, cancellationToken);
            return Ok(response);
        }

        [HttpPost("search")]
        [ProducesResponseType(typeof(SearchResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<SearchResponse>> Search(
            [FromBody] SearchRequest request,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request?.Query))
            {
                throw new ValidationException("Query is required.");
            }

            var response = await _documentSearchService.SearchAsync(request, cancellationToken);
            return Ok(response);
        }

        [HttpDelete("{documentId}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
        public async Task<ActionResult> Delete(
            string documentId,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(documentId))
            {
                throw new ValidationException("Document ID is required.");
            }

            await _documentStore.DeleteDocumentAsync(documentId, cancellationToken);
            return Ok(new { deleted = true });
        }
    }
}
