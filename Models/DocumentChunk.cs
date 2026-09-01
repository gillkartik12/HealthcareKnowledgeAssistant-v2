namespace HealthcareKnowledgeAssistant.Models
{
    public class DocumentChunk
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string DocumentId { get; set; } = "";
        public string Text { get; set; } = "";
        public string Source { get; set; } = "";
        public int PageNumber { get; set; }
        public int ChunkIndex { get; set; }
        public string Department { get; set; } = "";
        public string DocumentType { get; set; } = "";
    }
}
