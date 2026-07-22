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

        public DocumentsController(DocumentIngestionService documentIngestionService)
        {
            _documentIngestionService = documentIngestionService;
        }

        [HttpPost("upload")]
        [Consumes("multipart/form-data")]
        [ProducesResponseType(typeof(DocumentUploadResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<DocumentUploadResponse>> Upload(
            [FromForm] IFormFile? file,
            CancellationToken cancellationToken)
        {
            if (file is null)
            {
                return BadRequest(new { error = "File is required." });
            }

            try
            {
                var response = await _documentIngestionService.IngestAsync(file, cancellationToken);
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
    }
}
