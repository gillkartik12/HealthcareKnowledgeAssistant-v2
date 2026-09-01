using HealthcareKnowledgeAssistant.Models;
using UglyToad.PdfPig;

namespace HealthcareKnowledgeAssistant.Services;

public class PdfTextExtractor
{
    public List<DocumentPage> ExtractPages(string filePath, string sourceName)
    {
        var pages = new List<DocumentPage>();

        using var document = PdfDocument.Open(filePath);

        foreach (var page in document.GetPages())
        {
            if (string.IsNullOrWhiteSpace(page.Text))
                continue;

            pages.Add(new DocumentPage
            {
                Source = sourceName,
                PageNumber = page.Number,
                Text = page.Text
            });
        }

        return pages;
    }
}