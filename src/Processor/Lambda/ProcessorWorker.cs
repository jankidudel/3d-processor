using System.Text.Json;
using Amazon.Lambda.SQSEvents;
using Processor.Formats;
using Processor.Reporting;

namespace Processor.Lambda;

internal sealed class ProcessorWorker(
    IObjectStorage storage,
    ProcessorApplication processor,
    string bucketName,
    string jobsPrefix)
{
    internal const long MaxUploadSizeBytes = 25L * 1024 * 1024;
    internal const int MaxProcessingAttempts = 3;

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
                var permanentFailure = exception is ModelFormatException;
                if (permanentFailure || IsFinalAttempt(message))
                {
                    try
                    {
                        await WriteFailureAsync(message, exception, cancellationToken);
                        if (permanentFailure)
                        {
                            continue;
                        }
                    }
                    catch (Exception failureException)
                    {
                        log?.Invoke(
                            $"Writing failure result for SQS message {message.MessageId} failed: {failureException}");
                    }
                }
                failures.Add(new SQSBatchResponse.BatchItemFailure
                {
                    ItemIdentifier = message.MessageId
                });
            }
        }

        return new SQSBatchResponse(failures);
    }

    private async Task WriteFailureAsync(
        SQSEvent.SQSMessage message,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var s3Event = JsonSerializer.Deserialize<S3EventMessage>(message.Body, JsonOptions);
        var record = s3Event?.Records.FirstOrDefault();
        if (record is null || !string.Equals(record.S3.Bucket.Name, bucketName, StringComparison.Ordinal))
        {
            return;
        }
        var objectKey = DecodeObjectKey(record.S3.Object.Key);
        if (!TryGetJobId(objectKey, out var jobId))
        {
            return;
        }
        var failureJson = JsonSerializer.Serialize(new
        {
            reportVersion = "1.0",
            error = new
            {
                code = "processing_failed",
                message = exception is ModelFormatException formatException
                    ? formatException.ClientMessage
                    : "The file could not be processed."
            }
        });
        await storage.UploadJsonAsync(
            bucketName,
            $"{jobsPrefix}{jobId}/failure.json",
            failureJson,
            cancellationToken);
    }

    private bool TryGetJobId(string objectKey, out string jobId)
    {
        jobId = "";
        if (!objectKey.StartsWith(jobsPrefix, StringComparison.Ordinal))
        {
            return false;
        }
        var relativeKey = objectKey[jobsPrefix.Length..];
        var separatorIndex = relativeKey.IndexOf('/');
        if (separatorIndex <= 0 || !IsSupportedInputName(relativeKey[(separatorIndex + 1)..]))
        {
            return false;
        }
        jobId = relativeKey[..separatorIndex];
        return jobId is not "." and not ".." && !jobId.Contains('/');
    }

    private static bool IsFinalAttempt(SQSEvent.SQSMessage message)
    {
        return message.Attributes is not null &&
            message.Attributes.TryGetValue("ApproximateReceiveCount", out var rawCount) &&
            int.TryParse(rawCount, out var count) &&
            count >= MaxProcessingAttempts;
    }

    private async Task ProcessMessageAsync(
        SQSEvent.SQSMessage message,
        Action<string>? log,
        CancellationToken cancellationToken)
    {
        if (IsS3NotificationTestEvent(message.Body))
        {
            log?.Invoke($"Ignoring S3 notification test message {message.MessageId}.");
            return;
        }
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

    private static bool IsS3NotificationTestEvent(string messageBody)
    {
        try
        {
            using var document = JsonDocument.Parse(messageBody);
            return document.RootElement.TryGetProperty("Event", out var eventName) &&
                string.Equals(eventName.GetString(), "s3:TestEvent", StringComparison.Ordinal);
        }
        catch (JsonException)
        {
            return false;
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

        var relativeKey = objectKey.StartsWith(jobsPrefix, StringComparison.Ordinal)
            ? objectKey[jobsPrefix.Length..]
            : throw new InvalidDataException($"Object key '{objectKey}' is outside '{jobsPrefix}'.");
        var separatorIndex = relativeKey.IndexOf('/');
        if (separatorIndex <= 0 || !IsSupportedInputName(relativeKey[(separatorIndex + 1)..]))
        {
            throw new InvalidDataException(
                $"Object key '{objectKey}' must be a supported job input.");
        }

        var jobId = relativeKey[..separatorIndex];
        var fileName = relativeKey[(separatorIndex + 1)..];
        if (jobId is "." or ".." || jobId.Contains('/'))
        {
            throw new InvalidDataException(
                $"Object key '{objectKey}' must be a supported job input.");
        }

        if (!IsSupportedInputName(fileName))
        {
            throw new NotSupportedException($"File type for '{fileName}' is not supported.");
        }

        if (record.S3.Object.Size > MaxUploadSizeBytes)
        {
            throw new InvalidDataException(
                $"Object '{objectKey}' exceeds the 25 MiB processing limit.");
        }

        var currentObjectSize = await storage.GetSizeAsync(
            bucketName,
            objectKey,
            cancellationToken);
        if (currentObjectSize > MaxUploadSizeBytes)
        {
            throw new InvalidDataException(
                $"Object '{objectKey}' exceeds the 25 MiB processing limit.");
        }

        var temporaryPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}{Path.GetExtension(fileName)}");
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

            var isRepair = string.Equals(fileName, "repair.stl", StringComparison.Ordinal);
            var isThreeMfQualityChecker = string.Equals(fileName, "3mf_quality_checker.3mf", StringComparison.Ordinal);
            object? report = isRepair
                ? null
                : isThreeMfQualityChecker
                    ? processor.ProcessThreeMfQualityChecker(temporaryPath, fileName)
                    : processor.Process(temporaryPath, fileName);
            var repairOutputPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}-cleaned.stl");
            var repairReport = isRepair
                ? processor.RepairStl(temporaryPath, fileName, repairOutputPath)
                : null;
            var reportKey = $"{jobsPrefix}{jobId}/report.json";
            await storage.UploadJsonAsync(
                bucketName,
                reportKey,
                repairReport is not null
                    ? MeshQualityReportFormatter.FormatJson(repairReport)
                    : MeshQualityReportFormatter.FormatJson(report!),
                cancellationToken);
            if (repairReport?.HasCleanedFile == true)
            {
                await storage.UploadFileAsync(
                    bucketName,
                    $"{jobsPrefix}{jobId}/cleaned.stl",
                    repairOutputPath,
                    "model/stl",
                    cancellationToken);
            }
            File.Delete(repairOutputPath);
            log?.Invoke($"Processed s3://{bucketName}/{objectKey} to {reportKey}.");
        }
        finally
        {
            File.Delete(temporaryPath);
        }
    }

    private static bool IsSupportedInputName(string name)
    {
        return name is "input.stl" or "input.3mf" or "repair.stl" or "3mf_quality_checker.3mf";
    }

    private static string DecodeObjectKey(string objectKey)
    {
        return Uri.UnescapeDataString(objectKey.Replace('+', ' '));
    }
}
