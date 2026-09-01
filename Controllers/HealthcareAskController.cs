using HealthcareKnowledgeAssistant.Models;
using HealthcareKnowledgeAssistant.Services;
using HealthcareKnowledgeAssistant.Services.CacheService;
using Microsoft.AspNetCore.Mvc;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace HealthcareKnowledgeAssistant.Controllers;

[ApiController]
[Route("api/healthcare/ask")]
public class HealthcareAskController : ControllerBase
{
    private readonly GeminiEmbeddingService _embeddingService;
    private readonly GeminiChatService _chatService;
    private readonly QdrantService _qdrantService;
    private readonly HealthcareIntentService _intentService;
    private readonly RagLogService _ragLogService;
    private readonly DocumentChunkStore _chunkStore;
    private readonly KeywordSearchService _keywordSearchService;
    private readonly RerankingService _rerankingService;
    private readonly CacheService _cacheService;

    public HealthcareAskController(
        GeminiEmbeddingService embeddingService,
        GeminiChatService chatService,
        QdrantService qdrantService,
        HealthcareIntentService intentService,
        RagLogService ragLogService,
        DocumentChunkStore chunkStore,
        KeywordSearchService keywordSearchService,
        RerankingService rerankingService,
        CacheService cacheService)
    {
        _embeddingService = embeddingService;
        _chatService = chatService;
        _qdrantService = qdrantService;
        _intentService = intentService;
        _ragLogService = ragLogService;
        _chunkStore = chunkStore;
        _keywordSearchService = keywordSearchService;
        _rerankingService = rerankingService;
        _cacheService = cacheService;
    }
    private static string? NormalizeForCache(
        string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim().ToLowerInvariant();
    }

    private static string CreateCacheKey(
        string rewrittenQuestion,
        AskRequest request,
        string? department)
    {
        var cacheInput =
            JsonSerializer.Serialize(
                new
                {
                    Question =
                        rewrittenQuestion.Trim()
                            .ToLowerInvariant(),
                    request.TopK,
                    request.MinScore,
                    request.IncludeContext,
                    Source =
                        NormalizeForCache(request.Source),
                    Department =
                        NormalizeForCache(department),
                    DocumentType =
                        NormalizeForCache(request.DocumentType)
                });

        var bytes =
            SHA256.HashData(
                Encoding.UTF8.GetBytes(cacheInput));

        return $"rag:{Convert.ToHexString(bytes)}";
    }
    [HttpPost]
    public async Task<IActionResult> Ask([FromBody] AskRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Question))
            return BadRequest("Question is required.");

        var rewrittenQuestion = await _chatService.RewriteQuestionAsync(
            request.Question,
            request.ChatHistory);

        var department =
            !string.IsNullOrWhiteSpace(request.Department)
                ? request.Department
                : _intentService.DetectDepartment(request.Question);

        //add redis caching
        var cacheKey =
            CreateCacheKey(
                rewrittenQuestion,
                request,
                department);

        var cachedResponse =
            await _cacheService.GetAsync<object>(
                cacheKey);

        if (cachedResponse != null)
        {
            return Ok(cachedResponse);
        }


        //embedding
        var queryEmbedding = await _embeddingService
            .GenerateEmbeddingAsync(rewrittenQuestion);

        var vectorResults = await _qdrantService.SearchAsync(
         queryEmbedding,
         request.TopK,
         request.MinScore,
         request.Source,
         department,
         request.DocumentType);

        //Hybrid RAG search
        var keywordChunks = _chunkStore.GetChunks();

        if (!string.IsNullOrWhiteSpace(request.Source))
        {
            keywordChunks = keywordChunks
                .Where(x => x.Source.Equals(
                    request.Source,
                    StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        if (!string.IsNullOrWhiteSpace(department))
        {
            keywordChunks = keywordChunks
                .Where(x => x.Department.Equals(
                    department,
                    StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        if (!string.IsNullOrWhiteSpace(request.DocumentType))
        {
            keywordChunks = keywordChunks
                .Where(x => x.DocumentType.Equals(
                    request.DocumentType,
                    StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        var keywordResults = _keywordSearchService.Search(
            rewrittenQuestion,
            keywordChunks,
            request.TopK);

        var results = vectorResults
            .Concat(keywordResults)
            .GroupBy(x => $"{x.Source}-{x.PageNumber}-{x.Text}")
            .Select(g =>
            {
                var best = g.OrderByDescending(x => x.Score).First();

                best.RetrievalType = g.Count() > 1
                    ? "Hybrid"
                    : best.RetrievalType;

                return best;
            })
            .OrderByDescending(x => x.Score)
            .Take(request.TopK)
            .ToList();

        var usedFallbackSearch = false;


        if (!results.Any() && !string.IsNullOrWhiteSpace(department))
        {
            var fallbackVectorResults = await _qdrantService.SearchAsync(
                queryEmbedding,
                request.TopK,
                request.MinScore,
                request.Source,
                null,
                request.DocumentType);

            var fallbackKeywordChunks = _chunkStore.GetChunks();

            if (!string.IsNullOrWhiteSpace(request.Source))
            {
                fallbackKeywordChunks = fallbackKeywordChunks
                    .Where(x => x.Source.Equals(
                        request.Source,
                        StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }

            if (!string.IsNullOrWhiteSpace(request.DocumentType))
            {
                fallbackKeywordChunks = fallbackKeywordChunks
                    .Where(x => x.DocumentType.Equals(
                        request.DocumentType,
                        StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }

            var fallbackKeywordResults = _keywordSearchService.Search(
                rewrittenQuestion,
                fallbackKeywordChunks,
                request.TopK);

            results = fallbackVectorResults
                .Concat(fallbackKeywordResults)
                .GroupBy(x => $"{x.Source}-{x.PageNumber}-{x.Text}")
                .Select(g =>
                {
                    var best = g.OrderByDescending(x => x.Score).First();

                    best.RetrievalType = g.Count() > 1
                        ? "Hybrid"
                        : best.RetrievalType;

                    return best;
                })
                .OrderByDescending(x => x.Score)
                .Take(request.TopK)
                .ToList();

            usedFallbackSearch = true;
        }
        var retrievalSummary = new
        {
            vectorResults = results.Count(x => x.RetrievalType == "Vector"),
            keywordResults = results.Count(x => x.RetrievalType == "Keyword"),
            hybridResults = results.Count(x => x.RetrievalType == "Hybrid")
        };
        var resultsBeforeReranking = results.Count;

        results = await _rerankingService.RerankAsync(
            rewrittenQuestion,
            results,
            request.TopK);

        if (!results.Any())
        {
            return Ok(new
            {
                answer = "I could not find that information in the uploaded healthcare documents.",
                detectedDepartment = department,
                usedFallbackSearch,
                retrievalSummary,
                sources = Array.Empty<object>(),
                retrievedContext = Array.Empty<object>(),
            });
        }
        var bestScore = results.Max(x => x.Score);
        var confidence = GetConfidenceLabel(bestScore);
        var warning = confidence == "Low"
                    ? "This answer is based on weak retrieval matches. Please verify with the source document."
                    : null;

        var context = string.Join(
            "\n\n---\n\n",
            results.Select((x, index) =>
                $"Source {index + 1}: {x.Source}, page {x.PageNumber}\n{x.Text}")
        );

        var answer = await _chatService.GenerateHealthcareAnswerAsync(
            rewrittenQuestion,
            context);

        var sources = results
            .Select(x => new
            {
                x.Source,
                x.PageNumber,
                x.Score
            })
            .Distinct()
            .ToList();
        //add rag logging
        _ragLogService.Add(new RagLogItem
        {
            Question = request.Question,
            RewrittenQuestion = rewrittenQuestion,
            Answer = answer,
            Confidence = confidence,
            BestScore = bestScore,
            DetectedDepartment = department,
            UsedFallbackSearch = usedFallbackSearch,
            RetrievedChunksCount = results.Count,
            VectorResults = retrievalSummary.vectorResults,
            KeywordResults = retrievalSummary.keywordResults,
            HybridResults = retrievalSummary.hybridResults,
            RerankingApplied = true,
            ResultsBeforeReranking = resultsBeforeReranking,
            ResultsAfterReranking = results.Count
        });
        var finalResponse = new
        {
            answer,
            rewrittenQuestion,
            confidence,
            bestScore,
            warning,
            detectedDepartment = department,
            usedFallbackSearch,
            retrievalSummary,
            rerankingApplied = true,
            resultsBeforeReranking,
            resultsAfterReranking = results.Count,
            sources,
            retrievedContext = request.IncludeContext
                ? results.Select(x => new
                {
                    x.Text,
                    x.Source,
                    x.PageNumber,
                    x.Score,
                    x.RetrievalType
                })
                : null
        };
        // store caching response
        await _cacheService.SetAsync(
            cacheKey,
            finalResponse);

        return Ok(finalResponse);
    }
    private string GetConfidenceLabel(double bestScore)
    {
        if (bestScore >= 0.75)
            return "High";

        if (bestScore >= 0.50)
            return "Medium";

        return "Low";
    }
}
