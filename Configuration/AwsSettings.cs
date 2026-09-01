namespace HealthcareKnowledgeAssistant.Configuration
{
    public class AwsSettings
    {
        public const string SectionName = "AWS";

        public string ServiceURL { get; set; } = string.Empty;

        public string Region { get; set; } = "us-east-1";

        public string AccessKey { get; set; } = "test";

        public string SecretKey { get; set; } = "test";

        public string S3BucketName { get; set; } = string.Empty;
        public string SqsQueueName { get; set; } = "document-ingestion-queue";
        public string SqsDeadLetterQueueName { get; set; }
            = "document-ingestion-dlq";

        public int MaxReceiveCount { get; set; } = 3;

        public string DynamoDbTableName { get; set; }
            = "document-processing";
    }
}
