namespace HealthcareKnowledgeAssistant.Models
{
    public class DocumentIngestionMessage
    {
        public Guid DocumentId { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string ObjectKey { get; set; } = string.Empty;
        public string Department {  get; set; } = "General";
        public string DocumentType { get; set; } = "KnowledgeBase";
    }
}
