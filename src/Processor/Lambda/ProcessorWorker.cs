using System.Text.Json;
using Amazon.Lambda.SQSEvents;
using Processor.Reporting;

namespace Processor.Lambda;

internal sealed class ProcessorWorker(
    IObjectStorage storage,
    ProcessorApplication processor,
    string bucketName,
    string uploadsPrefix,
    string reportsPrefix)
{
    internal const long MaxUploadSizeBytes = 25L * 1024 * 1024;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<SQSBatchResponse> ProcessAsync(
        SQSEvent sqsEvent,
        Action<string>? log = null,
        CancellationToken cancellationToken = default)
    {
        var failures = new List<SQSBatchResponse.BatchItemFailure>();

        foreach (var message in sqsEvent.Records)
        {
            try
            {
                await ProcessMessageAsync(message, log, cancellationToken);
            }
            catch (Exception exception)
            {
                log?.Invoke($"Processing SQS message {message.MessageId} failed: {exception}");
                failures.Add(new SQSBatchResponse.BatchItemFailure
                {
                    ItemIdentifier = message.MessageId
                });
            }
        }

        return new SQSBatchResponse(failures);
    }

    private async Task ProcessMessageAsync(
        SQSEvent.SQSMessage message,
        Action<string>? log,
        CancellationToken cancellationToken)
    {
        var s3Event = JsonSerializer.Deserialize<S3EventMessage>(message.Body, JsonOptions)
            ?? throw new InvalidDataException("SQS message does not contain an S3 event.");

        if (s3Event.Records.Count == 0)
        {
            throw new InvalidDataException("S3 event contains no records.");
        }

        foreach (var record in s3Event.Records)
        {
            await ProcessObjectAsync(record, log, cancellationToken);
        }
    }

    private async Task ProcessObjectAsync(
        S3EventRecord record,
        Action<string>? log,
        CancellationToken cancellationToken)
    {
        var eventBucket = record.S3.Bucket.Name;
        var objectKey = DecodeObjectKey(record.S3.Object.Key);

        if (!string.Equals(eventBucket, bucketName, StringComparison.Ordinal))
        {
            throw new InvalidDataException($"Unexpected S3 bucket '{eventBucket}'.");
        }

        var relativeKey = objectKey.StartsWith(uploadsPrefix, StringComparison.Ordinal)
            ? objectKey[uploadsPrefix.Length..]
            : throw new InvalidDataException($"Object key '{objectKey}' is outside '{uploadsPrefix}'.");
        var separatorIndex = relativeKey.IndexOf('/');
        if (separatorIndex <= 0 || separatorIndex == relativeKey.Length - 1)
        {
            throw new InvalidDataException(
                $"Object key '{objectKey}' must match {uploadsPrefix}{{jobId}}/{{filename}}.");
        }

        var jobId = relativeKey[..separatorIndex];
        var fileName = relativeKey[(separatorIndex + 1)..];
        if (jobId is "." or ".." || fileName.Contains('/'))
        {
            throw new InvalidDataException(
                $"Object key '{objectKey}' must match {uploadsPrefix}{{jobId}}/{{filename}}.");
        }

        if (!string.Equals(Path.GetExtension(fileName), ".stl", StringComparison.OrdinalIgnoreCase))
        {
            throw new NotSupportedException($"File type for '{fileName}' is not supported.");
        }

        if (record.S3.Object.Size > MaxUploadSizeBytes)
        {
            throw new InvalidDataException(
                $"Object '{objectKey}' exceeds the 25 MiB processing limit.");
        }

        var temporaryPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.stl");
        try
        {
            await using (var temporaryFile = File.Create(temporaryPath))
            {
                await storage.DownloadAsync(
                    bucketName,
                    objectKey,
                    temporaryFile,
                    cancellationToken);
            }

            var report = processor.Process(temporaryPath, fileName);
            var reportKey = $"{reportsPrefix}{jobId}/report.json";
            await storage.UploadJsonAsync(
                bucketName,
                reportKey,
                StlQualityReportFormatter.FormatJson(report),
                cancellationToken);
            log?.Invoke($"Processed s3://{bucketName}/{objectKey} to {reportKey}.");
        }
        finally
        {
            File.Delete(temporaryPath);
        }
    }

    private static string DecodeObjectKey(string objectKey)
    {
        return Uri.UnescapeDataString(objectKey.Replace('+', ' '));
    }
}
