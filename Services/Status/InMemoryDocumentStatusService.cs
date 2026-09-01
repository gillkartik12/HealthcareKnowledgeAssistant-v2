using System.Collections.Concurrent;
using HealthcareKnowledgeAssistant.Models;

namespace HealthcareKnowledgeAssistant.Services.Status
{
    public class InMemoryDocumentStatusService : IDocumentStatusService
    {
        private readonly ConcurrentDictionary<Guid, DocumentProcessingStatus> _items = new();

        public Task CreateQueuedAsync(
            DocumentIngestionMessage message,
            CancellationToken cancellationToken = default)
        {
            var now = DateTime.UtcNow;

            _items[message.DocumentId] = new DocumentProcessingStatus
            {
                DocumentId = message.DocumentId,
                FileName = message.FileName,
                ObjectKey = message.ObjectKey,
                Status = "Queued",
                AttemptCount = 0,
                CreatedAt = now,
                UpdatedAt = now
            };

            return Task.CompletedTask;
        }

        public Task UpdateStatusAsync(
            Guid documentId,
            string status,
            int attemptCount,
            string? error = null,
            CancellationToken cancellationToken = default)
        {
            if (_items.TryGetValue(documentId, out var item))
            {
                item.Status = status;
                item.AttemptCount = attemptCount;
                item.Error = error;
                item.UpdatedAt = DateTime.UtcNow;
            }

            return Task.CompletedTask;
        }

        public Task<DocumentProcessingStatus?> GetAsync(
            Guid documentId,
            CancellationToken cancellationToken = default)
        {
            _items.TryGetValue(documentId, out var item);
            return Task.FromResult(item);
        }
    }
}
