using HealthcareKnowledgeAssistant.Configuration;
using HealthcareKnowledgeAssistant.Models;
using HealthcareKnowledgeAssistant.Services;
using HealthcareKnowledgeAssistant.Services.Messaging;
using HealthcareKnowledgeAssistant.Services.Status;
using HealthcareKnowledgeAssistant.Services.Storage;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace HealthcareKnowledgeAssistant.Controllers
{
    [ApiController]
    [Route("api/documents")]
    public class DocumentsController : ControllerBase
    {
        private readonly IObjectStorageService _storageService;
        private readonly IDocumentQueue _documentQueue;
        private readonly IDocumentStatusService _documentStatusService;
        private readonly DirectDocumentIngestionService _directIngestionService;
        private readonly QdrantService _qdrantService;
        private readonly DocumentChunkStore _chunkStore;
        private readonly CloudInfrastructureSettings _cloudSettings;
        private readonly ILogger<DocumentsController> _logger;

        public DocumentsController(
            QdrantService qdrantService,
            DocumentChunkStore chunkStore,
            IDocumentStatusService documentStatusService,
            DirectDocumentIngestionService directIngestionService,
            IObjectStorageService storageService,
            IDocumentQueue documentQueue,
            IOptions<CloudInfrastructureSettings> cloudOptions,
            ILogger<DocumentsController> logger)
        {
            _qdrantService = qdrantService;
            _chunkStore = chunkStore;
            _documentStatusService = documentStatusService;
            _directIngestionService = directIngestionService;
            _storageService = storageService;
            _documentQueue = documentQueue;
            _cloudSettings = cloudOptions.Value;
            _logger = logger;
        }

        [HttpDelete("reset")]
        public async Task<IActionResult> Reset()
        {
            await _qdrantService.ResetCollectionAsync();
            _chunkStore.Clear();

            return Ok(new
            {
                message = "Qdrant collection reset successfully."
            });
        }

        [HttpGet]
        public async Task<IActionResult> GetDocuments()
        {
            var sources = await _qdrantService.GetSourcesAsync();

            return Ok(new
            {
                documents = sources
            });
        }

        [HttpGet("stats")]
        public async Task<IActionResult> GetStats()
        {
            var stats = await _qdrantService.GetStatsAsync();
            return Ok(stats);
        }

        [HttpPost("upload")]
        public async Task<IActionResult> UploadPdf(
            IFormFile file,
            [FromForm] string department = "General",
            [FromForm] string documentType = "KnowledgeBase",
            CancellationToken cancellationToken = default)
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest(new { message = "No file uploaded." });
            }

            if (!file.FileName.EndsWith(
                    ".pdf",
                    StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new { message = "Only PDF files are supported." });
            }

            var documentId = Guid.NewGuid();
            var safeFileName = Path.GetFileName(file.FileName);

            if (_cloudSettings.Enabled)
            {
                return await UploadUsingCloudPipelineAsync(
                    file,
                    documentId,
                    safeFileName,
                    department,
                    documentType,
                    cancellationToken);
            }

            return await UploadUsingDirectPipelineAsync(
                file,
                documentId,
                safeFileName,
                department,
                documentType,
                cancellationToken);
        }

        private async Task<IActionResult> UploadUsingCloudPipelineAsync(
            IFormFile file,
            Guid documentId,
            string safeFileName,
            string department,
            string documentType,
            CancellationToken cancellationToken)
        {
            string objectKey;

            await using (var uploadStream = file.OpenReadStream())
            {
                objectKey = await _storageService.UploadAsync(
                    uploadStream,
                    safeFileName,
                    string.IsNullOrWhiteSpace(file.ContentType)
                        ? "application/pdf"
                        : file.ContentType,
                    cancellationToken);
            }

            var ingestionMessage = new DocumentIngestionMessage
            {
                DocumentId = documentId,
                FileName = safeFileName,
                ObjectKey = objectKey,
                Department = department,
                DocumentType = documentType
            };

            await _documentStatusService.CreateQueuedAsync(
                ingestionMessage,
                cancellationToken);

            await _documentQueue.SendAsync(
                ingestionMessage,
                cancellationToken);

            _logger.LogInformation(
                "Accepted document {DocumentId} for asynchronous cloud ingestion.",
                documentId);

            return Accepted(new
            {
                documentId,
                file = safeFileName,
                objectKey,
                department,
                documentType,
                status = "Queued",
                infrastructureMode = "CloudQueue"
            });
        }

        private async Task<IActionResult> UploadUsingDirectPipelineAsync(
            IFormFile file,
            Guid documentId,
            string safeFileName,
            string department,
            string documentType,
            CancellationToken cancellationToken)
        {
            // A logical object key keeps the status response shape identical to cloud mode.
            var objectKey = $"direct/{documentId:N}/{safeFileName}";

            var message = new DocumentIngestionMessage
            {
                DocumentId = documentId,
                FileName = safeFileName,
                ObjectKey = objectKey,
                Department = department,
                DocumentType = documentType
            };

            await _documentStatusService.CreateQueuedAsync(message, cancellationToken);
            await _documentStatusService.UpdateStatusAsync(
                documentId,
                "Processing",
                1,
                cancellationToken: cancellationToken);

            try
            {
                await using var stream = file.OpenReadStream();

                await _directIngestionService.ProcessAsync(
                    stream,
                    documentId,
                    safeFileName,
                    department,
                    documentType,
                    cancellationToken);

                await _documentStatusService.UpdateStatusAsync(
                    documentId,
                    "Completed",
                    1,
                    cancellationToken: cancellationToken);

                _logger.LogInformation(
                    "Completed direct ingestion for document {DocumentId}.",
                    documentId);

                // Direct mode completes the work before returning, so HTTP 200 is appropriate.
                return Ok(new
                {
                    documentId,
                    file = safeFileName,
                    objectKey,
                    department,
                    documentType,
                    status = "Completed",
                    infrastructureMode = "Direct"
                });
            }
            catch (Exception ex)
            {
                await _documentStatusService.UpdateStatusAsync(
                    documentId,
                    "Failed",
                    1,
                    ex.Message,
                    cancellationToken);

                _logger.LogError(
                    ex,
                    "Direct ingestion failed for document {DocumentId}.",
                    documentId);

                throw;
            }
        }

        [HttpGet("{documentId:guid}/status")]
        public async Task<IActionResult> GetDocumentStatus(
            Guid documentId,
            CancellationToken cancellationToken)
        {
            var status = await _documentStatusService.GetAsync(
                documentId,
                cancellationToken);

            if (status == null)
            {
                return NotFound(new
                {
                    message = "Document processing record was not found."
                });
            }

            return Ok(status);
        }
    }
}
