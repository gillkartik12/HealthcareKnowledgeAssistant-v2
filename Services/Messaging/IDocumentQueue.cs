using HealthcareKnowledgeAssistant.Models;

namespace HealthcareKnowledgeAssistant.Services.Messaging
{
    public interface IDocumentQueue
    {
        Task SendAsync(DocumentIngestionMessage message, CancellationToken ct = default);
    }
}
