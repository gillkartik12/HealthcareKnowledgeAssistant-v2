using HealthcareKnowledgeAssistant.Models;

namespace HealthcareKnowledgeAssistant.Services
{
    public class ChunkingService
    {
        public List<DocumentChunk> ChunkPages(List<DocumentPage> pages, string department,
            string documentType, int maxChars = 1200, int overlapChars = 200)
        {
            var chunks = new List<DocumentChunk>();
            foreach (var page in pages)
            {
                var paragraphs = page.Text
                    .Split(new[] { "\r\n\r\n", "\n\n" }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(p => p.Trim())
                    .Where(p => !string.IsNullOrWhiteSpace(p))
                    .ToList();

                var buffer = "";
                var chunkIndex = 0;

                foreach (var paragraph in paragraphs)
                {
                    if ((buffer.Length + paragraph.Length) <= maxChars)
                    {
                        buffer += paragraph + "\n\n";
                    }
                    else
                    {
                        if (!string.IsNullOrWhiteSpace(buffer))
                        {
                            chunks.Add(new DocumentChunk
                            {
                                Text = buffer.Trim(),
                                Source = page.Source,
                                PageNumber = page.PageNumber,
                                ChunkIndex = chunkIndex++,
                                Department = department,
                                DocumentType = documentType
                            });
                        }

                        buffer = paragraph + "\n\n";
                    }
                }

                if (!string.IsNullOrWhiteSpace(buffer))
                {
                    chunks.Add(new DocumentChunk
                    {
                        Text = buffer.Trim(),
                        Source = page.Source,
                        PageNumber = page.PageNumber,
                        ChunkIndex = chunkIndex,
                        Department = department,
                        DocumentType = documentType
                    });
                }
            }
            return chunks;
        }
    }
}
