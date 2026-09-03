namespace Processor.Lambda;

internal interface IObjectStorage
{
    Task<long> GetSizeAsync(
        string bucketName,
        string objectKey,
        CancellationToken cancellationToken = default);

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

    Task UploadFileAsync(
        string bucketName,
        string objectKey,
        string filePath,
        string contentType,
        CancellationToken cancellationToken = default);
}
