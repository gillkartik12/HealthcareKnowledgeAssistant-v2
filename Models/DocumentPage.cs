namespace HealthcareKnowledgeAssistant.Models
{
    public class DocumentPage
    {
        public string Source { get; set; } = string.Empty;
        public int PageNumber { get; set; }
        public string Text { get; set; } = "";
    }
}
