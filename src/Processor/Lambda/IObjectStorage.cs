namespace Processor.Lambda;

internal interface IObjectStorage
{
    Task DownloadAsync(
        string bucketName,
        string objectKey,
        Stream destination,
        CancellationToken cancellationToken = default);

    Task UploadJsonAsync(
        string bucketName,
        string objectKey,
        string json,
        CancellationToken cancellationToken = default);
}
