namespace HealthcareKnowledgeAssistant.Models;

public class SearchRequest
{
    private int _topK = 5;

    public string Question { get; set; } = "";
    public int TopK
    {
        get => _topK;
        set => _topK = value <= 0 ? 5 : Math.Min(value, 20);
    }
    public double MinScore { get; set; } = 0.70;
    public string? Source { get; set; }

    public string? Department { get; set; }

    public string? DocumentType { get; set; }
}
