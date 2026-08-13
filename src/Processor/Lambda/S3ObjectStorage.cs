using System.Text;
using Amazon.S3;
using Amazon.S3.Model;

namespace Processor.Lambda;

internal sealed class S3ObjectStorage(IAmazonS3 client) : IObjectStorage
{
    public async Task DownloadAsync(
        string bucketName,
        string objectKey,
        Stream destination,
        CancellationToken cancellationToken = default)
    {
        using var response = await client.GetObjectAsync(
            new GetObjectRequest { BucketName = bucketName, Key = objectKey },
            cancellationToken);
        await response.ResponseStream.CopyToAsync(destination, cancellationToken);
    }

    public async Task UploadJsonAsync(
        string bucketName,
        string objectKey,
        string json,
        CancellationToken cancellationToken = default)
    {
        await using var content = new MemoryStream(Encoding.UTF8.GetBytes(json));
        await client.PutObjectAsync(
            new PutObjectRequest
            {
                BucketName = bucketName,
                Key = objectKey,
                InputStream = content,
                ContentType = "application/json"
            },
            cancellationToken);
    }
}
