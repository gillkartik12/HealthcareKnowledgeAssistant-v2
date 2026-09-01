namespace HealthcareKnowledgeAssistant.Models;

public class SearchResult
{
    public string Text { get; set; } = "";
    public string Source { get; set; } = "";
    public int PageNumber { get; set; }
    public double Score { get; set; }
    public string RetrievalType { get; set; } = "";
}