using Amazon.S3;
using Amazon.S3.Model;
using HealthcareKnowledgeAssistant.Configuration;
using Microsoft.Extensions.Options;

namespace HealthcareKnowledgeAssistant.Infrastructure;

public class S3BucketInitializer
{
    private readonly IAmazonS3 _s3Client;
    private readonly AwsSettings _settings;
    private readonly ILogger<S3BucketInitializer> _logger;

    public S3BucketInitializer(
        IAmazonS3 s3Client,
        IOptions<AwsSettings> settings,
        ILogger<S3BucketInitializer> logger)
    {
        _s3Client = s3Client;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task InitializeAsync()
    {
        // Get all existing S3 buckets
        var response = await _s3Client.ListBucketsAsync();

        // Check if our bucket already exists
        bool bucketExists = response.Buckets?.Any(
                        bucket => string.Equals(
                            bucket.BucketName,
                            _settings.S3BucketName,
                            StringComparison.OrdinalIgnoreCase)) ?? false;

        if (bucketExists)
        {
            _logger.LogInformation(
                "S3 bucket {Bucket} already exists.",
                _settings.S3BucketName);

            return;
        }

        await _s3Client.PutBucketAsync(
            new PutBucketRequest
            {
                BucketName = _settings.S3BucketName,
                BucketRegionName = _settings.Region
            });

        _logger.LogInformation(
            "Created S3 bucket {Bucket}.",
            _settings.S3BucketName);
    }
}