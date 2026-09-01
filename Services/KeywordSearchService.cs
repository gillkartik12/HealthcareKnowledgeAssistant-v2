using HealthcareKnowledgeAssistant.Models;
using System.Text.RegularExpressions;

namespace HealthcareKnowledgeAssistant.Services;

public class KeywordSearchService
{
    public List<SearchResult> Search(
        string query,
        List<DocumentChunk> chunks,
        int topK = 5)
    {
        var queryWords = ExtractWords(query);

        if (!queryWords.Any())
            return new List<SearchResult>();

        var rawResults = chunks
            .Select(chunk =>
            {
                var text = chunk.Text.ToLowerInvariant();

                var matchCount = queryWords.Count(word =>
                    text.Contains(word));

                var keywordScore =
                    (double)matchCount / queryWords.Count;

                return new SearchResult
                {
                    Text = chunk.Text,
                    Source = chunk.Source,
                    PageNumber = chunk.PageNumber,
                    Score = keywordScore,
                    RetrievalType = "Keyword"
                };
            })
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .Take(topK)
            .ToList();

        return rawResults;
    }

    private List<string> ExtractWords(string query)
    {
        return Regex
            .Matches(query.ToLowerInvariant(), @"[a-z0-9]+")
            .Select(match => match.Value)
            .Where(word => word.Length > 2)
            .Distinct()
            .ToList();
    }
}