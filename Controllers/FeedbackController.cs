
using HealthcareKnowledgeAssistant.Models;
using HealthcareKnowledgeAssistant.Services;
using Microsoft.AspNetCore.Mvc;

namespace HealthcareKnowledgeAssistant.Controllers;

[ApiController]
[Route("api/feedback")]
public class FeedbackController : ControllerBase
{
    private readonly FeedbackService _feedbackService;

    public FeedbackController(FeedbackService feedbackService)
    {
        _feedbackService = feedbackService;
    }

    [HttpPost]
    public IActionResult AddFeedback([FromBody] FeedbackItem item)
    {
        _feedbackService.Add(item);

        return Ok(new
        {
            message = "Feedback saved.",
            item
        });
    }

    [HttpGet]
    public IActionResult GetFeedback()
    {
        return Ok(_feedbackService.GetAll());
    }

    [HttpDelete]
    public IActionResult ClearFeedback()
    {
        _feedbackService.Clear();

        return Ok(new
        {
            message = "Feedback cleared."
        });
    }
    [HttpGet("summary")]
    public IActionResult GetSummary()
    {
        return Ok(_feedbackService.GetSummary());
    }
    [HttpGet("export")]
    public IActionResult ExportFeedback()
    {
        var csv = _feedbackService.ExportCsv();

        return File(
            System.Text.Encoding.UTF8.GetBytes(csv),
            "text/csv",
            "rag-feedback.csv");
    }
}