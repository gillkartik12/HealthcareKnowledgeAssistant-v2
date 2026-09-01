namespace HealthcareKnowledgeAssistant.Models;

public class FeedbackItem
{
    public string Question { get; set; } = "";
    public string Answer { get; set; } = "";
    public string Rating { get; set; } = "";
    public string? Confidence { get; set; }
    public double? BestScore { get; set; }
    public object? Sources { get; set; }
    public string? RewrittenQuestion { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}