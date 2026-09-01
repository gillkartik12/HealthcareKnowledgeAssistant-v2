using HealthcareKnowledgeAssistant.Models;

namespace HealthcareKnowledgeAssistant.Services.Status
{
    public interface IDocumentStatusService
    {
        Task CreateQueuedAsync(
            DocumentIngestionMessage message,
            CancellationToken cancellationToken = default);

        Task UpdateStatusAsync(
            Guid documentId,
            string status,
            int attemptCount,
            string? error = null,
            CancellationToken cancellationToken = default);

        Task<DocumentProcessingStatus?> GetAsync(
            Guid documentId,
            CancellationToken cancellationToken = default);
    }
}
