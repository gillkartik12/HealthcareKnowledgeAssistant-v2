namespace HealthcareKnowledgeAssistant.Models;

public class RagLogItem
{
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    public string Question { get; set; } = "";

    public string? RewrittenQuestion { get; set; }

    public string Answer { get; set; } = "";

    public string? Confidence { get; set; }

    public double BestScore { get; set; }

    public string? DetectedDepartment { get; set; }

    public bool UsedFallbackSearch { get; set; }

    public int RetrievedChunksCount { get; set; }

    public int VectorResults { get; set; }

    public int KeywordResults { get; set; }

    public int HybridResults { get; set; }
    public bool RerankingApplied { get; set; }
    public int ResultsBeforeReranking { get; set; }
    public int ResultsAfterReranking { get; set; }
}