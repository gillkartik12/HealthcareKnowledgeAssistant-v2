namespace HealthcareKnowledgeAssistant.Models;

public class AskRequest
{
    private int _topK = 5;

    public string Question { get; set; } = "";
    public int TopK
    {
        get => _topK;
        set => _topK = value <= 0 ? 5 : Math.Min(value, 20);
    }
    public double MinScore { get; set; } = 0.5;
    public bool IncludeContext { get; set; } = true;
    public string? Source { get; set; }
    public string? Department { get; set; }
    public string? DocumentType { get; set; }
    public List<ChatMessage> ChatHistory { get; set; } = new();
    public class ChatMessage
    {
        public string Question { get; set; } = "";
        public string Answer { get; set; } = "";
    }

}
