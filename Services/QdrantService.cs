using HealthcareKnowledgeAssistant.Models;
using Microsoft.AspNetCore.Mvc;
using Qdrant.Client;
using Qdrant.Client.Grpc;

namespace HealthcareKnowledgeAssistant.Services;

public class QdrantService
{
    private const string CollectionName = "document_chunks";
    private const ulong VectorSize = 3072;

    private readonly QdrantClient _client;

    public QdrantService(IConfiguration configuration)
    {
        var host = configuration["Qdrant:Host"] ?? "localhost";
        var port = int.TryParse(configuration["Qdrant:Port"], out var p) ? p : 6334;
        var useHttps = bool.TryParse(configuration["Qdrant:UseHttps"], out var h) && h;
        var apiKey = configuration["Qdrant:ApiKey"];

        _client = new QdrantClient(
            host,
            port,
            https: useHttps,
            apiKey: string.IsNullOrWhiteSpace(apiKey) ? null : apiKey);
    }

    public async Task EnsureCollectionAsync()
    {
        var collections = await _client.ListCollectionsAsync();

        if (collections.Contains(CollectionName))
            return;

        await _client.CreateCollectionAsync(
            collectionName: CollectionName,
            vectorsConfig: new VectorParams
            {
                Size = VectorSize,
                Distance = Distance.Cosine
            });

        // Payload indexes are required for filtered search on Qdrant Cloud
        // (strict mode) and improve filter performance everywhere.
        await _client.CreatePayloadIndexAsync(CollectionName, "source", PayloadSchemaType.Keyword);
        await _client.CreatePayloadIndexAsync(CollectionName, "documentId", PayloadSchemaType.Keyword);
        await _client.CreatePayloadIndexAsync(CollectionName, "department", PayloadSchemaType.Keyword);
        await _client.CreatePayloadIndexAsync(CollectionName, "documentType", PayloadSchemaType.Keyword);
    }
    public async Task<DocumentStats> GetStatsAsync()
    {
        await EnsureCollectionAsync();

        var scrollResult = await _client.ScrollAsync(
            collectionName: CollectionName,
            limit: 1000
        );

        var documents = scrollResult.Result
        .Where(point => point.Payload.ContainsKey("source"))
        .GroupBy(point => new
        {
            DocumentId = point.Payload.ContainsKey("documentId")
                ? point.Payload["documentId"].StringValue
                : "",
            Source = point.Payload["source"].StringValue,
            Department = point.Payload.ContainsKey("department")
                ? point.Payload["department"].StringValue
                : "Unknown",
            DocumentType = point.Payload.ContainsKey("documentType")
                ? point.Payload["documentType"].StringValue
                : "Unknown"
        })
        .Select(group => new DocumentStatsItem
        {
            DocumentId = string.IsNullOrWhiteSpace(group.Key.DocumentId)
                ? group.Key.Source
                : group.Key.DocumentId,
            Source = group.Key.Source,
            Department = group.Key.Department,
            DocumentType = group.Key.DocumentType,
            Chunks = group.Count()
        })
        .OrderBy(x => x.Source)
        .ToList();

        return new DocumentStats
        {
            TotalChunks = documents.Sum(x => x.Chunks),
            Documents = documents
        };
    }
    public async Task<List<string>> GetSourcesAsync()
    {
        await EnsureCollectionAsync();

        var scrollResult = await _client.ScrollAsync(
            collectionName: CollectionName,
            limit: 1000
        );

        return scrollResult.Result
            .Where(point => point.Payload.ContainsKey("source"))
            .Select(point => point.Payload["source"].StringValue)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(source => source)
            .ToList();
    }
    public async Task<List<SearchResult>> SearchAsync(
    float[] queryVector,
    int topK,
    double minScore = 0.70,
    string? source = null,
    string? department = null,
    string? documentType = null)
    {
        await EnsureCollectionAsync();

        var normalizedTopK =
            topK <= 0
                ? 5
                : Math.Min(topK, 20);

        Filter? filter = null;

        var conditions = new List<Condition>();

        if (!string.IsNullOrWhiteSpace(source))
        {
            conditions.Add(new Condition
            {
                Field = new FieldCondition
                {
                    Key = "source",
                    Match = new Match { Keyword = source }
                }
            });
        }

        if (!string.IsNullOrWhiteSpace(department))
        {
            conditions.Add(new Condition
            {
                Field = new FieldCondition
                {
                    Key = "department",
                    Match = new Match { Keyword = department }
                }
            });
        }

        if (!string.IsNullOrWhiteSpace(documentType))
        {
            conditions.Add(new Condition
            {
                Field = new FieldCondition
                {
                    Key = "documentType",
                    Match = new Match { Keyword = documentType }
                }
            });
        }

        if (conditions.Any())
        {
            filter = new Filter();
            filter.Must.AddRange(conditions);
        }

        var results = await _client.SearchAsync(
            collectionName: CollectionName,
            vector: queryVector,
            filter: filter,
            limit: (ulong)(normalizedTopK * 3)
        );

        return results
            .Where(result => result.Score >= minScore)
            .OrderByDescending(result => result.Score)
            .GroupBy(result =>
            {
                var documentKey =
                    result.Payload.ContainsKey("documentId") &&
                    !string.IsNullOrWhiteSpace(result.Payload["documentId"].StringValue)
                        ? result.Payload["documentId"].StringValue
                        : result.Payload["source"].StringValue;

                return $"{documentKey}-{result.Payload["pageNumber"].IntegerValue}";
            })
            .Select(group => group.First())
            .Take(normalizedTopK)
            .Select(result => new SearchResult
            {
                Text = result.Payload["text"].StringValue,
                Source = result.Payload["source"].StringValue,
                PageNumber = (int)result.Payload["pageNumber"].IntegerValue,
                Score = result.Score,
                RetrievalType = "Vector"
            })
            .ToList();
    }
    public async Task ResetCollectionAsync()
    {
        var collections = await _client.ListCollectionsAsync();

        if (collections.Contains(CollectionName))
        {
            await _client.DeleteCollectionAsync(CollectionName);
        }

        await EnsureCollectionAsync();
    }
    public async Task UpsertChunksAsync(List<DocumentChunk> chunks, List<float[]> embeddings)
    {
        await EnsureCollectionAsync();

        if (!chunks.Any())
        {
            throw new ArgumentException(
                "At least one document chunk is required for Qdrant upsert.",
                nameof(chunks));
        }

        if (embeddings.Count != chunks.Count)
        {
            throw new ArgumentException(
                "The embedding count must match the document chunk count.",
                nameof(embeddings));
        }

        var points = chunks.Select((chunk, index) => new PointStruct
        {
            Id = Guid.Parse(chunk.Id),
            Vectors = embeddings[index],
            Payload =
            {
                ["documentId"] = chunk.DocumentId,
                ["text"] = chunk.Text,
                ["source"] = chunk.Source,
                ["pageNumber"] = chunk.PageNumber,
                ["chunkIndex"] = chunk.ChunkIndex,
                ["department"] = chunk.Department,
                ["documentType"] = chunk.DocumentType
            }
        }).ToList();

        await _client.UpsertAsync(CollectionName, points);
    }
}
