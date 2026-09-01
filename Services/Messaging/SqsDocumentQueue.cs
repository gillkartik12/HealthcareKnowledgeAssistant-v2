using System.Text.Json;
using System.Threading;
using Amazon.SQS;
using Amazon.SQS.Model;
using HealthcareKnowledgeAssistant.Configuration;
using HealthcareKnowledgeAssistant.Models;
using Microsoft.Extensions.Options;

namespace HealthcareKnowledgeAssistant.Services.Messaging
{
    public class SqsDocumentQueue : IDocumentQueue
    {
        public readonly IAmazonSQS _sqsClient;
        public readonly AwsSettings _awsSettings;
        public readonly ILogger<SqsDocumentQueue> _logger;

        private string? _queueUrl;

        public SqsDocumentQueue(IAmazonSQS sqsClient, IOptions<AwsSettings> options, ILogger<SqsDocumentQueue> logger)
        {
            _sqsClient = sqsClient;
            _awsSettings = options.Value;
            _logger = logger;
        }

        public async Task SendAsync(DocumentIngestionMessage message, CancellationToken ct = default)
        {
            var queueUrl =
                await GetQueueUrlAsync(ct);
            var messageBody = JsonSerializer.Serialize(message);
            var request = new SendMessageRequest
            {
                QueueUrl = queueUrl,
                MessageBody = messageBody
            };

            await _sqsClient.SendMessageAsync(request, ct);

            _logger.LogInformation(
                "Queued document {DocumentId} for ingestion", message.DocumentId);
        }
        private async Task<string> GetQueueUrlAsync(
            CancellationToken cancellationToken)
        {
            if (!string.IsNullOrWhiteSpace(_queueUrl))
            {
                return _queueUrl;
            }

            var response =
                await _sqsClient.GetQueueUrlAsync(
                    new GetQueueUrlRequest
                    {
                        QueueName =
                            _awsSettings.SqsQueueName
                    },
                    cancellationToken);

            _queueUrl = response.QueueUrl;

            return _queueUrl;
        }
    }
}
