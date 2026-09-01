using HealthcareKnowledgeAssistant.Models;

namespace HealthcareKnowledgeAssistant.Services;

public class RagLogService
{
    private readonly List<RagLogItem> _items;
    private readonly string _filePath;
    private readonly object _lock = new();

    public RagLogService(IHostEnvironment env)
    {
        _filePath = JsonFileStore.GetDataPath(env, "rag-logs.json");
        _items = JsonFileStore.Load<RagLogItem>(_filePath);
    }

    public void Add(RagLogItem item)
    {
        lock (_lock)
        {
            item.Timestamp = DateTime.UtcNow;
            _items.Add(item);
            JsonFileStore.Save(_filePath, _items);
        }
    }

    public List<RagLogItem> GetAll()
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

        var high = items.Count(x => x.Confidence == "High");
        var medium = items.Count(x => x.Confidence == "Medium");
        var low = items.Count(x => x.Confidence == "Low");

        var fallbackSearches = items.Count(x => x.UsedFallbackSearch);

        var averageBestScore = total == 0
            ? 0
            : Math.Round(items.Average(x => x.BestScore), 4);

        var totalVectorResults = items.Sum(x => x.VectorResults);
        var totalKeywordResults = items.Sum(x => x.KeywordResults);
        var totalHybridResults = items.Sum(x => x.HybridResults);

        var rerankedQuestions = items.Count(x => x.RerankingApplied);

        return new
        {
            totalQuestions = total,
            highConfidence = high,
            mediumConfidence = medium,
            lowConfidence = low,
            fallbackSearches,
            averageBestScore,
            totalVectorResults,
            totalKeywordResults,
            totalHybridResults,
            rerankedQuestions
        };
    }
    public string ExportCsv()
    {
        var lines = new List<string>
    {
        "Timestamp,Question,RewrittenQuestion,Answer,Confidence,BestScore,DetectedDepartment,UsedFallbackSearch,RetrievedChunksCount,VectorResults,KeywordResults,HybridResults"
    };

        foreach (var item in GetAll())
        {
            lines.Add(string.Join(",", new[]
            {
            EscapeCsv(item.Timestamp.ToString("O")),
            EscapeCsv(item.Question),
            EscapeCsv(item.RewrittenQuestion ?? ""),
            EscapeCsv(item.Answer),
            EscapeCsv(item.Confidence ?? ""),
            EscapeCsv(item.BestScore.ToString()),
            EscapeCsv(item.DetectedDepartment ?? ""),
            EscapeCsv(item.UsedFallbackSearch.ToString()),
            EscapeCsv(item.RetrievedChunksCount.ToString()),
            EscapeCsv(item.VectorResults.ToString()),
            EscapeCsv(item.KeywordResults.ToString()),
            EscapeCsv(item.HybridResults.ToString())
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