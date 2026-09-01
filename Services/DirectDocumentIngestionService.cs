using System.Security.Cryptography;
using System.Text;
using HealthcareKnowledgeAssistant.Models;

namespace HealthcareKnowledgeAssistant.Services
{
    /// <summary>
    /// Direct ingestion path used by hosts that do not provide the local AWS/Redis
    /// infrastructure. It preserves the same PDF -> chunk -> embedding -> Qdrant
    /// behavior without requiring S3, SQS or DynamoDB.
    /// </summary>
    public class DirectDocumentIngestionService
    {
        private readonly PdfTextExtractor _pdfTextExtractor;
        private readonly ChunkingService _chunkingService;
        private readonly GeminiEmbeddingService _embeddingService;
        private readonly QdrantService _qdrantService;
        private readonly DocumentChunkStore _chunkStore;

        public DirectDocumentIngestionService(
            PdfTextExtractor pdfTextExtractor,
            ChunkingService chunkingService,
            GeminiEmbeddingService embeddingService,
            QdrantService qdrantService,
            DocumentChunkStore chunkStore)
        {
            _pdfTextExtractor = pdfTextExtractor;
            _chunkingService = chunkingService;
            _embeddingService = embeddingService;
            _qdrantService = qdrantService;
            _chunkStore = chunkStore;
        }

        public async Task ProcessAsync(
            Stream pdfStream,
            Guid documentId,
            string fileName,
            string department,
            string documentType,
            CancellationToken cancellationToken = default)
        {
            var tempFilePath = Path.Combine(
                Path.GetTempPath(),
                $"{documentId}.pdf");

            try
            {
                await using (var fileStream = File.Create(tempFilePath))
                {
                    await pdfStream.CopyToAsync(fileStream, cancellationToken);
                }

                var pages = _pdfTextExtractor.ExtractPages(tempFilePath, fileName);

                if (!pages.Any())
                {
                    throw new InvalidOperationException(
                        "No extractable text was found in the PDF. Upload a text-based PDF or run OCR before ingestion.");
                }

                var chunks = _chunkingService.ChunkPages(
                    pages,
                    department,
                    documentType);

                if (!chunks.Any())
                {
                    throw new InvalidOperationException(
                        "No document chunks were created from the PDF text.");
                }

                foreach (var chunk in chunks)
                {
                    chunk.DocumentId = documentId.ToString();
                    chunk.Id = CreateDeterministicChunkId(
                        documentId,
                        chunk.PageNumber,
                        chunk.ChunkIndex);
                }

                var embeddings = new List<float[]>(chunks.Count);

                foreach (var chunk in chunks)
                {
                    embeddings.Add(
                        await _embeddingService.GenerateEmbeddingAsync(chunk.Text));
                }

                await _qdrantService.UpsertChunksAsync(chunks, embeddings);
                _chunkStore.AddChunks(chunks);
            }
            finally
            {
                if (File.Exists(tempFilePath))
                {
                    File.Delete(tempFilePath);
                }
            }
        }

        private static string CreateDeterministicChunkId(
            Guid documentId,
            int pageNumber,
            int chunkIndex)
        {
            var input = $"{documentId:N}:{pageNumber}:{chunkIndex}";
            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(input));
            return new Guid(hash.Take(16).ToArray()).ToString();
        }
    }
}
