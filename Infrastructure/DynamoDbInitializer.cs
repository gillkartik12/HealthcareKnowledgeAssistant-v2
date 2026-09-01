using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using HealthcareKnowledgeAssistant.Configuration;
using Microsoft.Extensions.Options;

namespace HealthcareKnowledgeAssistant.Infrastructure
{
    public class DynamoDbInitializer
    {
        private readonly IAmazonDynamoDB _dynamoDb;
        private readonly AwsSettings _settings;
        private readonly ILogger<DynamoDbInitializer> _logger;

        public DynamoDbInitializer(IAmazonDynamoDB dynamoDb, IOptions<AwsSettings> options, ILogger<DynamoDbInitializer> logger)
        {
            _dynamoDb = dynamoDb;
            _settings = options.Value;
            _logger = logger;
        }
        public async Task InitializeAsync()
        {
            var tables = await _dynamoDb.ListTablesAsync();

            if (tables.TableNames.Contains(_settings.DynamoDbTableName))
            {
                _logger.LogInformation("DynamoDb table {TableName} already exists",
                    _settings.DynamoDbTableName);

                return;
            }

            await _dynamoDb.CreateTableAsync(
                new CreateTableRequest
                {
                    TableName =
                        _settings.DynamoDbTableName,

                    KeySchema = [
                            new KeySchemaElement{
                                AttributeName = "DocumentId",
                                KeyType = KeyType.HASH
                            }
                        ],
                    AttributeDefinitions =
                    [
                        new AttributeDefinition
                        {
                            AttributeName = "DocumentId",
                            AttributeType =
                                ScalarAttributeType.S
                        }
                    ],
                    BillingMode =
                        BillingMode.PAY_PER_REQUEST

                });
            _logger.LogInformation(
               "Created DynamoDB table {TableName}.",
               _settings.DynamoDbTableName);
        }
    }
}
