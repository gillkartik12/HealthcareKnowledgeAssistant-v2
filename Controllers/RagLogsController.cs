using HealthcareKnowledgeAssistant.Services;
using Microsoft.AspNetCore.Mvc;

namespace HealthcareKnowledgeAssistant.Controllers;

[ApiController]
[Route("api/rag-logs")]
public class RagLogsController : ControllerBase
{
    private readonly RagLogService _ragLogService;

    public RagLogsController(RagLogService ragLogService)
    {
        _ragLogService = ragLogService;
    }

    [HttpGet]
    public IActionResult GetLogs()
    {
        return Ok(_ragLogService.GetAll());
    }

    [HttpDelete]
    public IActionResult ClearLogs()
    {
        _ragLogService.Clear();

        return Ok(new
        {
            message = "RAG logs cleared."
        });
    }
    [HttpGet("summary")]
    public IActionResult GetSummary()
    {
        return Ok(_ragLogService.GetSummary());
    }
    [HttpGet("export")]
    public IActionResult ExportLogs()
    {
        var csv = _ragLogService.ExportCsv();

        return File(
            System.Text.Encoding.UTF8.GetBytes(csv),
            "text/csv",
            "rag-logs.csv");
    }
}