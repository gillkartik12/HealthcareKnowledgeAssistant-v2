using HealthcareKnowledgeAssistant.Models;

namespace HealthcareKnowledgeAssistant.Services;

public class FeedbackService
{
    private readonly List<FeedbackItem> _items;
    private readonly string _filePath;
    private readonly object _lock = new();

    public FeedbackService(IHostEnvironment env)
    {
        _filePath = JsonFileStore.GetDataPath(env, "feedback.json");
        _items = JsonFileStore.Load<FeedbackItem>(_filePath);
    }

    public void Add(FeedbackItem item)
    {
        lock (_lock)
        {
            item.Timestamp = DateTime.UtcNow;
            _items.Add(item);
            JsonFileStore.Save(_filePath, _items);
        }
    }

    public List<FeedbackItem> GetAll()
    {
        lock (_lock)
        {
            return _items
                .OrderByDescending(x => x.Timestamp)
                .ToList();
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            _items.Clear();
            JsonFileStore.Save(_filePath, _items);
        }
    }
    public object GetSummary()
    {
        var items = GetAll();

        var total = items.Count;

        var helpful = items.Count(x => x.Rating == "Helpful");

        var notHelpful = items.Count(x => x.Rating == "Not Helpful");

        var helpfulRate = total == 0
            ? 0
            : Math.Round((double)helpful / total * 100, 2);

        return new
        {
            total,
            helpful,
            notHelpful,
            helpfulRate
        };
    }
    public string ExportCsv()
    {
        var lines = new List<string>
    {
        "Timestamp,Question,RewrittenQuestion,Rating,Confidence,BestScore,Answer"
    };

        foreach (var item in GetAll())
        {
            lines.Add(string.Join(",", new[]
            {
            EscapeCsv(item.Timestamp.ToString("O")),
            EscapeCsv(item.Question),
            EscapeCsv(item.RewrittenQuestion ?? ""),
            EscapeCsv(item.Rating),
            EscapeCsv(item.Confidence ?? ""),
            EscapeCsv(item.BestScore?.ToString() ?? ""),
            EscapeCsv(item.Answer)
        }));
        }

        return string.Join(Environment.NewLine, lines);
    }

    private string EscapeCsv(string value)
    {
        if (value.Contains(",") || value.Contains("\"") || value.Contains("\n"))
        {
            value = value.Replace("\"", "\"\"");
            return $"\"{value}\"";
        }

        return value;
    }
}