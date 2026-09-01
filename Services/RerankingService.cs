using System.Text;
using System.Text.Json;
using HealthcareKnowledgeAssistant.Models;

namespace HealthcareKnowledgeAssistant.Services;

public class RerankingService
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly ILogger<RerankingService> _logger;

    public RerankingService(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<RerankingService> logger)
    {
        _httpClient = httpClient;
        _apiKey = configuration["Gemini:ApiKey"]
            ?? throw new Exception("Gemini API key missing.");
        _logger = logger;
    }

    public async Task<List<SearchResult>> RerankAsync(
        string question,
        List<SearchResult> results,
        int topK)
    {
        var normalizedTopK =
            topK <= 0
                ? 5
                : Math.Min(topK, 20);

        if (!results.Any())
            return results;

        var chunkText = string.Join(
            "\n\n",
            results.Select((x, index) =>
                $"Chunk {index + 1}:\n{x.Text}")
        );

        var prompt = $"""
        You are a reranking model.

        Your task is to rank document chunks by how well they answer the user's question.

        Return ONLY a JSON array of chunk numbers in best-to-worst order.

        Example:
        [3,1,2]

        Question:
        {question}

        Chunks:
        {chunkText}
        """;

        var requestBody = new
        {
            contents = new[]
            {
                new
                {
                    parts = new[]
                    {
                        new { text = prompt }
                    }
                }
            }
        };

        var json = JsonSerializer.Serialize(requestBody);

        try
        {
            var response = await _httpClient.PostAsync(
                $"https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent?key={_apiKey}",
                new StringContent(json, Encoding.UTF8, "application/json"));

            response.EnsureSuccessStatusCode();

            var responseJson = await response.Content.ReadAsStringAsync();

            using var doc = JsonDocument.Parse(responseJson);

            var text = doc.RootElement
                .GetProperty("candidates")[0]
                .GetProperty("content")
                .GetProperty("parts")[0]
                .GetProperty("text")
                .GetString() ?? "";

            var indexes = JsonSerializer.Deserialize<List<int>>(
                text.Trim().Replace("```json", "").Replace("```", ""));

            if (indexes == null || !indexes.Any())
                return results.Take(normalizedTopK).ToList();

            var reranked = indexes
                .Where(i => i >= 1 && i <= results.Count)
                .Select(i => results[i - 1])
                .Take(normalizedTopK)
                .ToList();

            return reranked.Any()
                ? reranked
                : results.Take(normalizedTopK).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Reranking failed. Falling back to original retrieval order.");

            return results.Take(normalizedTopK).ToList();
        }
    }
}
