using Amazon.DynamoDBv2;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.SQS;
using HealthcareKnowledgeAssistant.Configuration;
using HealthcareKnowledgeAssistant.Services;
using HealthcareKnowledgeAssistant.Services.CacheService;
using HealthcareKnowledgeAssistant.Services.Messaging;
using HealthcareKnowledgeAssistant.Services.Status;
using HealthcareKnowledgeAssistant.Services.Storage;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

// ---------------- Configuration ----------------
builder.Services.Configure<CloudInfrastructureSettings>(
    builder.Configuration.GetSection(CloudInfrastructureSettings.SectionName));

builder.Services.Configure<AwsSettings>(
    builder.Configuration.GetSection(AwsSettings.SectionName));

builder.Services.Configure<RedisSettings>(
    builder.Configuration.GetSection(RedisSettings.SectionName));

var cloudSettings = builder.Configuration
    .GetSection(CloudInfrastructureSettings.SectionName)
    .Get<CloudInfrastructureSettings>()
    ?? new CloudInfrastructureSettings();

var awsSettings = builder.Configuration
    .GetSection(AwsSettings.SectionName)
    .Get<AwsSettings>()
    ?? new AwsSettings();

var redisSettings = builder.Configuration
    .GetSection(RedisSettings.SectionName)
    .Get<RedisSettings>()
    ?? new RedisSettings();

// ---------------- AWS clients ----------------
// Clients are registered in both modes so controllers have a stable dependency graph.
// In Monster/direct mode they are never called, so localhost LocalStack is not required.
var credentials = new BasicAWSCredentials(
    awsSettings.AccessKey,
    awsSettings.SecretKey);

var s3Config = new AmazonS3Config
{
    ServiceURL = awsSettings.ServiceURL,
    ForcePathStyle = true,
    AuthenticationRegion = awsSettings.Region
};

builder.Services.AddSingleton<IAmazonS3>(
    new AmazonS3Client(credentials, s3Config));
builder.Services.AddSingleton<IObjectStorageService, S3ObjectStorageService>();

var sqsConfig = new AmazonSQSConfig
{
    ServiceURL = awsSettings.ServiceURL,
    AuthenticationRegion = awsSettings.Region
};

builder.Services.AddSingleton<IAmazonSQS>(
    new AmazonSQSClient(credentials, sqsConfig));
builder.Services.AddSingleton<IDocumentQueue, SqsDocumentQueue>();

var dynamoDbConfig = new AmazonDynamoDBConfig
{
    ServiceURL = awsSettings.ServiceURL,
    AuthenticationRegion = awsSettings.Region
};

builder.Services.AddSingleton<IAmazonDynamoDB>(
    new AmazonDynamoDBClient(credentials, dynamoDbConfig));

// Local cloud mode persists status in DynamoDB and runs the SQS worker.
// Monster/direct mode keeps short-lived status in memory and processes immediately.
if (cloudSettings.Enabled)
{
    builder.Services.AddSingleton<IDocumentStatusService, DocumentStatusService>();
    builder.Services.AddHostedService<DocumentIngestionWorker>();
}
else
{
    builder.Services.AddSingleton<IDocumentStatusService, InMemoryDocumentStatusService>();
}

// ---------------- Redis cache ----------------
// Redis is optional. Monster mode leaves it disabled and RAG continues without caching.
if (redisSettings.Enabled)
{
    builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
    {
        var options = ConfigurationOptions.Parse(redisSettings.ConnectionString);
        options.AbortOnConnectFail = false;
        options.ConnectTimeout = 2500;
        options.SyncTimeout = 2500;
        return ConnectionMultiplexer.Connect(options);
    });
}

builder.Services.AddSingleton<CacheService>();

// ---------------- Application services ----------------
builder.Services.AddSingleton<PdfTextExtractor>();
builder.Services.AddSingleton<ChunkingService>();
builder.Services.AddSingleton<QdrantService>();
builder.Services.AddSingleton<HealthcareIntentService>();
builder.Services.AddSingleton<FeedbackService>();
builder.Services.AddSingleton<RagLogService>();
builder.Services.AddSingleton<DocumentChunkStore>();
builder.Services.AddSingleton<KeywordSearchService>();
builder.Services.AddSingleton<DirectDocumentIngestionService>();

builder.Services
    .AddHttpClient<GeminiEmbeddingService>()
    .AddStandardResilienceHandler(options =>
    {
        options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(30);
        options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(10);
        options.Retry.MaxRetryAttempts = 3;
        options.Retry.Delay = TimeSpan.FromSeconds(1);
        options.Retry.BackoffType = Polly.DelayBackoffType.Exponential;
    });

builder.Services
    .AddHttpClient<GeminiChatService>()
    .AddStandardResilienceHandler(options =>
    {
        options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(30);
        options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(10);
        options.Retry.MaxRetryAttempts = 3;
        options.Retry.Delay = TimeSpan.FromSeconds(1);
        options.Retry.BackoffType = Polly.DelayBackoffType.Exponential;
    });

builder.Services
    .AddHttpClient<RerankingService>()
    .AddStandardResilienceHandler(options =>
    {
        options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(30);
        options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(10);
        options.Retry.MaxRetryAttempts = 3;
        options.Retry.Delay = TimeSpan.FromSeconds(1);
        options.Retry.BackoffType = Polly.DelayBackoffType.Exponential;
    });

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.Use(async (context, next) =>
{
    try
    {
        await next(context);
    }
    catch (Exception ex) when (!context.Response.HasStarted)
    {
        var logger = context.RequestServices
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger("GlobalExceptionHandler");

        logger.LogError(
            ex,
            "Unhandled exception while processing {Method} {Path}.",
            context.Request.Method,
            context.Request.Path);

        var isQuotaError =
            ex.Message.Contains("RESOURCE_EXHAUSTED", StringComparison.OrdinalIgnoreCase) ||
            ex.Message.Contains("Quota exceeded", StringComparison.OrdinalIgnoreCase) ||
            ex.Message.Contains("429", StringComparison.OrdinalIgnoreCase);

        context.Response.StatusCode = isQuotaError
            ? StatusCodes.Status429TooManyRequests
            : StatusCodes.Status503ServiceUnavailable;

        await context.Response.WriteAsJsonAsync(new
        {
            message = isQuotaError
                ? "The AI provider quota has been reached. Please retry after the quota window resets."
                : "The request could not be completed because a downstream service is unavailable.",
            statusCode = context.Response.StatusCode
        });
    }
});

app.UseHttpsRedirection();
app.UseDefaultFiles();
app.UseStaticFiles();
app.UseAuthorization();
app.MapControllers();

app.Run();
