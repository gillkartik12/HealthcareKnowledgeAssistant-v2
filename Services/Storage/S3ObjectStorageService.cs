using Amazon.S3;
using Amazon.S3.Model;
using HealthcareKnowledgeAssistant.Configuration;
using Microsoft.Extensions.Options;

namespace HealthcareKnowledgeAssistant.Services.Storage
{
    public class S3ObjectStorageService : IObjectStorageService
    {
        private readonly IAmazonS3 _s3Client;
        private readonly AwsSettings _awsSettings;
        private readonly ILogger<S3ObjectStorageService> _logger;

        public S3ObjectStorageService(
            IAmazonS3 s3Client,
            IOptions<AwsSettings> options,
            ILogger<S3ObjectStorageService> logger)
        {
            _s3Client = s3Client;
            _awsSettings = options.Value;
            _logger = logger;
        }

        public async Task<string> UploadAsync(
            Stream stream,
            string fileName,
            string contentType,
            CancellationToken cancellationToken = default)
        {
            var documentId = Guid.NewGuid();

            var safeFileName =
                Path.GetFileName(fileName);

            var objectKey =
                $"documents/{documentId}/{safeFileName}";

            var request = new PutObjectRequest
            {
                BucketName = _awsSettings.S3BucketName,
                Key = objectKey,
                InputStream = stream,
                ContentType = contentType
            };

            await _s3Client.PutObjectAsync(
                request,
                cancellationToken);

            _logger.LogInformation(
                "Uploaded document {ObjectKey} to S3 bucket {Bucket}",
                objectKey,
                _awsSettings.S3BucketName);

            return objectKey;
        }

        public async Task<Stream> DownloadAsync(
            string objectKey,
            CancellationToken cancellationToken = default)
        {
            var request = new GetObjectRequest
            {
                BucketName = _awsSettings.S3BucketName,
                Key = objectKey
            };

            using var response =
                await _s3Client.GetObjectAsync(
                    request,
                    cancellationToken);

            var memoryStream =
                new MemoryStream();

            await response.ResponseStream.CopyToAsync(
                memoryStream,
                cancellationToken);

            memoryStream.Position = 0;

            return memoryStream;
        }

        public async Task DeleteAsync(
            string objectKey,
            CancellationToken cancellationToken = default)
        {
            var request = new DeleteObjectRequest
            {
                BucketName = _awsSettings.S3BucketName,
                Key = objectKey
            };

            await _s3Client.DeleteObjectAsync(
                request,
                cancellationToken);

            _logger.LogInformation(
                "Deleted document {ObjectKey} from S3 bucket {Bucket}",
                objectKey,
                _awsSettings.S3BucketName);
        }
    }
}