using HealthcareKnowledgeAssistant.Models;
using HealthcareKnowledgeAssistant.Services;
using Microsoft.AspNetCore.Mvc;

namespace HealthcareKnowledgeAssistant.Controllers;

[ApiController]
[Route("api/ask")]
public class AskController : ControllerBase
{
    private readonly GeminiEmbeddingService _embeddingService;
    private readonly GeminiChatService _chatService;
    private readonly QdrantService _qdrantService;

    public AskController(
        GeminiEmbeddingService embeddingService,
        GeminiChatService chatService,
        QdrantService qdrantService)
    {
        _embeddingService = embeddingService;
        _chatService = chatService;
        _qdrantService = qdrantService;
    }

    [HttpPost]
    public async Task<IActionResult> Ask([FromBody] AskRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Question))
            return BadRequest("Question is required.");

        var queryEmbedding = await _embeddingService
            .GenerateEmbeddingAsync(request.Question);

        var results = await _qdrantService.SearchAsync(
            queryEmbedding,
            request.TopK,
            request.MinScore,
            request.Source,
            request.Department,
            request.DocumentType);

        var strongResults = results
    .Where(x => x.Score >= request.MinScore)
    .ToList();

        if (!strongResults.Any())
        {
            return Ok(new
            {
                answer = "I could not find that information.",
                sources = Array.Empty<object>(),
                retrievedContext = Array.Empty<object>()
            });
        }

        var context = string.Join(
            "\n\n---\n\n",
            strongResults.Select((x, index) =>
                $"Source {index + 1}: {x.Source}, page {x.PageNumber}\n{x.Text}")
        );

        var answer = await _chatService.GenerateAnswerAsync(
            request.Question,
            context);

        var sources = strongResults
            .Select(x => new
            {
                x.Source,
                x.PageNumber,
                x.Score
            })
            .Distinct()
            .ToList();

        return Ok(new
        {
            answer,
            sources,
            retrievedContext = request.IncludeContext
                ? strongResults.Select(x => new
                {
                    x.Text,
                    x.Source,
                    x.PageNumber,
                    x.Score
                })
                : null
        });
    }
}
