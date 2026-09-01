
using Amazon.SQS;
using Amazon.SQS.Model;
using HealthcareKnowledgeAssistant.Configuration;
using HealthcareKnowledgeAssistant.Models;
using HealthcareKnowledgeAssistant.Services.Storage;
using HealthcareKnowledgeAssistant.Services.Status;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace HealthcareKnowledgeAssistant.Services
{
    public class DocumentIngestionWorker : BackgroundService
    {
        private readonly IAmazonSQS _sqsClient;
        private readonly IObjectStorageService _storageService;
        private readonly PdfTextExtractor _pdfTextExtractor;
        private readonly ChunkingService _chunkingService;
        private readonly GeminiEmbeddingService _embeddingService;
        private readonly QdrantService _qdrantService;
        private readonly DocumentChunkStore _chunkStore;
        private readonly AwsSettings _settings;
        private readonly ILogger<DocumentIngestionWorker> _logger;
        private readonly IDocumentStatusService _documentStatusService;

        private string? _queueUrl;

        public DocumentIngestionWorker(
            IAmazonSQS sqsClient,
            IObjectStorageService storageService,
            PdfTextExtractor pdfTextExtractor,
            ChunkingService chunkingService,
            GeminiEmbeddingService embeddingService,
            QdrantService qdrantService,
            DocumentChunkStore chunkStore,
            IOptions<AwsSettings> options,
            ILogger<DocumentIngestionWorker> logger,
            IDocumentStatusService documentStatusService)
        {
            _sqsClient = sqsClient;
            _storageService = storageService;
            _pdfTextExtractor = pdfTextExtractor;
            _chunkingService = chunkingService;
            _embeddingService = embeddingService;
            _qdrantService = qdrantService;
            _chunkStore = chunkStore;
            _settings = options.Value;
            _logger = logger;
            _documentStatusService = documentStatusService;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation(
                "Document ingestion worker started.");

            _queueUrl = await GetQueueUrlAsync(stoppingToken);

            while(!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await PollQueueAsync(stoppingToken);
                }
                catch (OperationCanceledException)
                    when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch(Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "Unexpected error while polling document ingestion queue.");

                    await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                }
            }
        }
        private async Task PollQueueAsync(CancellationToken cancellationToken)
        {
            var request = new ReceiveMessageRequest
            {
                QueueUrl = _queueUrl,
                MaxNumberOfMessages = 1,

                WaitTimeSeconds = 10,

                VisibilityTimeout = 60,

                MessageSystemAttributeNames =
                new List<string>
                {
                    "ApproximateReceiveCount"
                }

            };
            var response =
             await _sqsClient.ReceiveMessageAsync(
                 request,
                 cancellationToken);
            if (response.Messages is null || response.Messages.Count == 0)
            {
                return;
            }

            foreach (var message in response.Messages)
            {
                await ProcessMessageAsync(message, cancellationToken);
            }
        }
        private async Task ProcessMessageAsync(Message message, CancellationToken cancellationToken)
        {
            var attemptCount = 1;

            if (message.Attributes.TryGetValue(
                    "ApproximateReceiveCount",
                    out var receiveCount))
            {
                int.TryParse(
                    receiveCount,
                    out attemptCount);
            }

            if (attemptCount <= 0)
            {
                attemptCount = 1;
            }


            DocumentIngestionMessage? ingestionMessage;
            try
            {
                ingestionMessage = JsonSerializer.Deserialize<DocumentIngestionMessage>(message.Body);
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Could not deserialize SQS message {MessageId}.", message.MessageId);

                return;
            }
            if (ingestionMessage == null)
            {
                _logger.LogWarning("SQS message {MessageId} has no usable body.", message.MessageId);
                return;
            }

            try
            {
                _logger.LogInformation(
                    "Starting ingestion for document {DocumentId}: {FileName}",
                    ingestionMessage.DocumentId,
                    ingestionMessage.FileName);

                await _documentStatusService
                    .UpdateStatusAsync(
                        ingestionMessage.DocumentId,
                        "Processing",
                        attemptCount,
                        cancellationToken:
                            cancellationToken);

                await ProcessDocumentAsync(
                    ingestionMessage,
                    cancellationToken);

                await _documentStatusService
                    .UpdateStatusAsync(
                        ingestionMessage.DocumentId,
                        "Completed",
                        attemptCount,
                        cancellationToken:
                            cancellationToken);

                await DeleteMessageAsync(
                    message,
                    cancellationToken);

                _logger.LogInformation(
                    "Completed ingestion for document {DocumentId}.",
                    ingestionMessage.DocumentId);
            }
            catch (Exception ex)
            {
                var finalAttempt =
                    attemptCount >=
                    _settings.MaxReceiveCount;

                var status =
                    finalAttempt
                        ? "Failed"
                        : "Retrying";

                await _documentStatusService
                    .UpdateStatusAsync(
                        ingestionMessage.DocumentId,
                        status,
                        attemptCount,
                        ex.Message,
                        cancellationToken);

                _logger.LogError(
                    ex,
                    "Document {DocumentId} failed on attempt {Attempt}/{MaxAttempts}. Status: {Status}",
                    ingestionMessage.DocumentId,
                    attemptCount,
                    _settings.MaxReceiveCount,
                    status);

                // IMPORTANT:
                // Do NOT delete the SQS message.
            }
        }
        private static string CreateDeterministicChunkId(
    Guid documentId,
    int pageNumber,
    int chunkIndex)
        {
            var input =
                $"{documentId:N}:{pageNumber}:{chunkIndex}";

            var inputBytes =
                Encoding.UTF8.GetBytes(input);

            var hash =
                SHA256.HashData(inputBytes);

            var guidBytes =
                hash.Take(16).ToArray();

            return new Guid(guidBytes)
                .ToString();
        }
        private async Task ProcessDocumentAsync(
            DocumentIngestionMessage message,
            CancellationToken cancellationToken)
        {
            await using var s3Stream =
                await _storageService.DownloadAsync(
                    message.ObjectKey,
                    cancellationToken);

            var tempFilePath =
                Path.Combine(
                    Path.GetTempPath(),
                    $"{message.DocumentId}.pdf");

            try
            {
                await using (
                    var fileStream =
                        File.Create(tempFilePath))
                {
                    await s3Stream.CopyToAsync(
                        fileStream,
                        cancellationToken);
                }

                var pages =
                    _pdfTextExtractor.ExtractPages(
                        tempFilePath,
                        message.FileName);

                if (!pages.Any())
                {
                    throw new InvalidOperationException(
                        "No extractable text was found in the PDF. Upload a text-based PDF or run OCR before ingestion.");
                }

                var chunks =
                    _chunkingService.ChunkPages(
                        pages,
                        message.Department,
                        message.DocumentType);

                if (!chunks.Any())
                {
                    throw new InvalidOperationException(
                        "No document chunks were created from the PDF text.");
                }

                foreach (var chunk in chunks)
                {
                    chunk.DocumentId =
                        message.DocumentId.ToString();

                    chunk.Id =
                        CreateDeterministicChunkId(
                            message.DocumentId,
                            chunk.PageNumber,
                            chunk.ChunkIndex);
                }

                var embeddings =
                    new List<float[]>();

                foreach (var chunk in chunks)
                {
                    var embedding =
                        await _embeddingService
                            .GenerateEmbeddingAsync(
                                chunk.Text);

                    embeddings.Add(
                        embedding);
                }

                await _qdrantService
                    .UpsertChunksAsync(
                        chunks,
                        embeddings);

                _chunkStore.AddChunks(
                    chunks);
            }
            finally
            {
                if (File.Exists(tempFilePath))
                {
                    File.Delete(tempFilePath);
                }
            }
        }

        private async Task DeleteMessageAsync(
            Message message,
            CancellationToken cancellationToken)
        {
            await _sqsClient.DeleteMessageAsync(
                new DeleteMessageRequest
                {
                    QueueUrl = _queueUrl,
                    ReceiptHandle =
                        message.ReceiptHandle
                },
                cancellationToken);
        }



        private async Task<string> GetQueueUrlAsync(
            CancellationToken cancellationToken)
        {
            var response =
                await _sqsClient.GetQueueUrlAsync(
                    new GetQueueUrlRequest
                    {
                        QueueName =
                            _settings.SqsQueueName
                    },
                    cancellationToken);

            return response.QueueUrl;
        }
    }
}
