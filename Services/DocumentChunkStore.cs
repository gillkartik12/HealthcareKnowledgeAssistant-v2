using HealthcareKnowledgeAssistant.Models;

namespace HealthcareKnowledgeAssistant.Services;

public class DocumentChunkStore
{
    private readonly List<DocumentChunk> _chunks;
    private readonly string _filePath;
    private readonly object _lock = new();

    public DocumentChunkStore(IHostEnvironment env)
    {
        _filePath = JsonFileStore.GetDataPath(env, "chunks.json");
        _chunks = JsonFileStore.Load<DocumentChunk>(_filePath);
    }

    public void AddChunks(List<DocumentChunk> chunks)
    {
        lock (_lock)
        {
            // SQS retries for the same upload should replace that upload's chunks.
            var documentIds = chunks
                .Select(x => x.DocumentId)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            _chunks.RemoveAll(x => documentIds.Contains(x.DocumentId));
            _chunks.AddRange(chunks);
            JsonFileStore.Save(_filePath, _chunks);
        }
    }

    public List<DocumentChunk> GetChunks()
    {
        lock (_lock)
        {
            return _chunks.ToList();
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            _chunks.Clear();
            JsonFileStore.Save(_filePath, _chunks);
        }
    }
}
