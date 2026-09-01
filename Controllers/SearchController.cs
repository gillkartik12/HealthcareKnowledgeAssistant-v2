using HealthcareKnowledgeAssistant.Models;
using HealthcareKnowledgeAssistant.Services;
using Microsoft.AspNetCore.Mvc;

namespace HealthcareKnowledgeAssistant.Controllers
{
    [ApiController]
    [Route("/api/search")]
    public class SearchController : ControllerBase
    {
        private readonly GeminiEmbeddingService _geminiEmbeddingService;
        private readonly QdrantService _qdrantService;

        public SearchController(GeminiEmbeddingService geminiEmbeddingService, QdrantService qdrantService)
        {
            _geminiEmbeddingService = geminiEmbeddingService;
            _qdrantService = qdrantService;
        }
        [HttpPost]
        public async Task<IActionResult> Search([FromBody] SearchRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Question))
                return BadRequest("Question is required.");

            var queryEmbedding = await _geminiEmbeddingService.GenerateEmbeddingAsync(request.Question);

            var results = await _qdrantService.SearchAsync(
                queryEmbedding,
                request.TopK,
                request.MinScore,
                request.Source,
                request.Department,
                request.DocumentType
            );

            return Ok(new
            {
                question = request.Question,
                results
            });
        }
    }
}
