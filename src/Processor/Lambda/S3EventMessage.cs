using System.Text.Json.Serialization;

namespace Processor.Lambda;

internal sealed class S3EventMessage
{
    [JsonPropertyName("Records")]
    public List<S3EventRecord> Records { get; init; } = [];
}

internal sealed class S3EventRecord
{
    [JsonPropertyName("s3")]
    public S3Entity S3 { get; init; } = new();
}

internal sealed class S3Entity
{
    [JsonPropertyName("bucket")]
    public S3BucketEntity Bucket { get; init; } = new();

    [JsonPropertyName("object")]
    public S3ObjectEntity Object { get; init; } = new();
}

internal sealed class S3BucketEntity
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = "";
}

internal sealed class S3ObjectEntity
{
    [JsonPropertyName("key")]
    public string Key { get; init; } = "";
}
