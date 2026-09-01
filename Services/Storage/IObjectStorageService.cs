namespace HealthcareKnowledgeAssistant.Services.Storage
{
    public interface IObjectStorageService
    {
        Task<string> UploadAsync(Stream stream,
            string fileName,
            string contentType,
            CancellationToken cancellationToken = default);

        Task<Stream> DownloadAsync(
            string objectKey,
            CancellationToken cancellationToken = default);

        Task DeleteAsync(
            string objectKey,
            CancellationToken cancellationToken = default);


    }
}
