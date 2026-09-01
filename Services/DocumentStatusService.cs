using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using HealthcareKnowledgeAssistant.Configuration;
using HealthcareKnowledgeAssistant.Models;
using Microsoft.Extensions.Options;
using HealthcareKnowledgeAssistant.Services.Status;

namespace HealthcareKnowledgeAssistant.Services
{
    public class DocumentStatusService : IDocumentStatusService
    {
        private readonly IAmazonDynamoDB _dynamoDb;
        private readonly AwsSettings _settings;

        public DocumentStatusService(
            IAmazonDynamoDB dynamoDb,
            IOptions<AwsSettings> options)
        {
            _dynamoDb = dynamoDb;
            _settings = options.Value;
        }

        public async Task CreateQueuedAsync(
            DocumentIngestionMessage message,
            CancellationToken cancellationToken = default)
        {
            var now =
                DateTime.UtcNow.ToString("O");

            var item =
                new Dictionary<string, AttributeValue>
                {
                    ["DocumentId"] =
                        new AttributeValue
                        {
                            S = message.DocumentId.ToString()
                        },

                    ["FileName"] =
                        new AttributeValue
                        {
                            S = message.FileName
                        },

                    ["ObjectKey"] =
                        new AttributeValue
                        {
                            S = message.ObjectKey
                        },

                    ["Status"] =
                        new AttributeValue
                        {
                            S = "Queued"
                        },

                    ["AttemptCount"] =
                        new AttributeValue
                        {
                            N = "0"
                        },

                    ["CreatedAt"] =
                        new AttributeValue
                        {
                            S = now
                        },

                    ["UpdatedAt"] =
                        new AttributeValue
                        {
                            S = now
                        }
                };

            await _dynamoDb.PutItemAsync(
                new PutItemRequest
                {
                    TableName =
                        _settings.DynamoDbTableName,

                    Item = item
                },
                cancellationToken);
        }

        public async Task UpdateStatusAsync(
            Guid documentId,
            string status,
            int attemptCount,
            string? error = null,
            CancellationToken cancellationToken = default)
        {
            var expressionValues =
                new Dictionary<string, AttributeValue>
                {
                    [":status"] =
                        new AttributeValue
                        {
                            S = status
                        },

                    [":attempt"] =
                        new AttributeValue
                        {
                            N = attemptCount.ToString()
                        },

                    [":updated"] =
                        new AttributeValue
                        {
                            S = DateTime.UtcNow
                                .ToString("O")
                        }
                };

            var updateExpression =
                "SET #status = :status, " +
                "AttemptCount = :attempt, " +
                "UpdatedAt = :updated";

            var expressionAttributeNames =
                new Dictionary<string, string>
                {
                    ["#status"] = "Status"
                };

            if (!string.IsNullOrWhiteSpace(error))
            {
                updateExpression += ", #error = :error";

                expressionAttributeNames["#error"] =
                    "Error";

                expressionValues[":error"] =
                    new AttributeValue
                    {
                        S = error
                    };
            }
            else
            {
                updateExpression += " REMOVE #error";

                expressionAttributeNames["#error"] =
                    "Error";
            }

            await _dynamoDb.UpdateItemAsync(
                new UpdateItemRequest
                {
                    TableName =
                        _settings.DynamoDbTableName,

                    Key =
                        new Dictionary<string, AttributeValue>
                        {
                            ["DocumentId"] =
                                new AttributeValue
                                {
                                    S = documentId.ToString()
                                }
                        },

                    UpdateExpression =
                        updateExpression,

                    ExpressionAttributeNames =
                        expressionAttributeNames,

                    ExpressionAttributeValues =
                        expressionValues
                },
                cancellationToken);
        }

        public async Task<DocumentProcessingStatus?>
            GetAsync(
                Guid documentId,
                CancellationToken cancellationToken = default)
        {
            var response =
                await _dynamoDb.GetItemAsync(
                    new GetItemRequest
                    {
                        TableName =
                            _settings.DynamoDbTableName,

                        Key =
                            new Dictionary<string, AttributeValue>
                            {
                                ["DocumentId"] =
                                    new AttributeValue
                                    {
                                        S = documentId
                                            .ToString()
                                    }
                            }
                    },
                    cancellationToken);

            if (response.Item == null ||
                response.Item.Count == 0)
            {
                return null;
            }

            var item = response.Item;

            return new DocumentProcessingStatus
            {
                DocumentId = documentId,

                FileName =
                    item.TryGetValue(
                        "FileName",
                        out var fileName)
                        ? fileName.S
                        : string.Empty,

                ObjectKey =
                    item.TryGetValue(
                        "ObjectKey",
                        out var objectKey)
                        ? objectKey.S
                        : string.Empty,

                Status =
                    item.TryGetValue(
                        "Status",
                        out var status)
                        ? status.S
                        : "Unknown",

                AttemptCount =
                    item.TryGetValue(
                        "AttemptCount",
                        out var attempts) &&
                    int.TryParse(
                        attempts.N,
                        out var attemptCount)
                        ? attemptCount
                        : 0,

                Error =
                    item.TryGetValue(
                        "Error",
                        out var error)
                        ? error.S
                        : null,

                CreatedAt =
                    item.TryGetValue(
                        "CreatedAt",
                        out var created) &&
                    DateTime.TryParse(
                        created.S,
                        out var createdAt)
                        ? createdAt
                        : DateTime.MinValue,

                UpdatedAt =
                    item.TryGetValue(
                        "UpdatedAt",
                        out var updated) &&
                    DateTime.TryParse(
                        updated.S,
                        out var updatedAt)
                        ? updatedAt
                        : DateTime.MinValue
            };
        }
    }
}
