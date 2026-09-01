namespace HealthcareKnowledgeAssistant.Models
{
    public class DocumentProcessingStatus
    {
        public Guid DocumentId { get; set; }

        public string FileName { get; set; } = string.Empty;

        public string ObjectKey { get; set; } = string.Empty;

        public string Status { get; set; } = "Queued";

        public int AttemptCount { get; set; }

        public string? Error { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime UpdatedAt { get; set; }
    }
}