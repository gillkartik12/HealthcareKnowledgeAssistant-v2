using Amazon.SQS;
using Amazon.SQS.Model;
using HealthcareKnowledgeAssistant.Configuration;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace HealthcareKnowledgeAssistant.Infrastructure
{
    public class SqsQueueInitializer
    {
        private readonly IAmazonSQS _sqsClient;
        private readonly AwsSettings _settings;
        private readonly ILogger<SqsQueueInitializer> _logger;

        public SqsQueueInitializer(
            IAmazonSQS sqsClient,
            IOptions<AwsSettings> options,
            ILogger<SqsQueueInitializer> logger)
        {
            _sqsClient = sqsClient;
            _settings = options.Value;
            _logger = logger;
        }

        public async Task InitializeAsync()
        {
            // 1. Create DLQ first
            var dlqUrl = await EnsureQueueExistsAsync(
                _settings.SqsDeadLetterQueueName);

            // 2. Get DLQ ARN
            var dlqAttributes =
                await _sqsClient.GetQueueAttributesAsync(
                    new GetQueueAttributesRequest
                    {
                        QueueUrl = dlqUrl,

                        AttributeNames =
                            new List<string>
                            {
                                "QueueArn"
                            }
                    });

            var dlqArn =
                dlqAttributes.Attributes["QueueArn"];

            // 3. Create main ingestion queue
            var queueUrl =
                await EnsureQueueExistsAsync(
                    _settings.SqsQueueName);

            // 4. Configure redrive policy
            var redrivePolicy =
                JsonSerializer.Serialize(
                    new Dictionary<string, string>
                    {
                        ["deadLetterTargetArn"] = dlqArn,

                        ["maxReceiveCount"] =
                            _settings.MaxReceiveCount
                                .ToString()
                    });

            await _sqsClient.SetQueueAttributesAsync(
                new SetQueueAttributesRequest
                {
                    QueueUrl = queueUrl,

                    Attributes =
                        new Dictionary<string, string>
                        {
                            ["RedrivePolicy"] =
                                redrivePolicy
                        }
                });

            _logger.LogInformation(
                "Configured SQS queue {QueueName} with DLQ {DlqName}.",
                _settings.SqsQueueName,
                _settings.SqsDeadLetterQueueName);
        }
        private async Task<string> EnsureQueueExistsAsync(
            string queueName)
        {
            try
            {
                var response =
                    await _sqsClient.GetQueueUrlAsync(
                        new GetQueueUrlRequest
                        {
                            QueueName = queueName
                        });

                return response.QueueUrl;
            }
            catch (QueueDoesNotExistException)
            {
                var response =
                    await _sqsClient.CreateQueueAsync(
                        new CreateQueueRequest
                        {
                            QueueName = queueName
                        });

                _logger.LogInformation(
                    "Created SQS queue {QueueName}.",
                    queueName);

                return response.QueueUrl;
            }
        }
    }
}