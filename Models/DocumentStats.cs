namespace HealthcareKnowledgeAssistant.Models;

public class DocumentStats
{
    public int TotalChunks { get; set; }
    public List<DocumentStatsItem> Documents { get; set; } = new();
}

public class DocumentStatsItem
{
    public string DocumentId { get; set; } = "";
    public string Source { get; set; } = "";
    public string Department { get; set; } = "";
    public string DocumentType { get; set; } = "";
    public int Chunks { get; set; }
}
