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
    private const string InputFolder = "input";
    private const string OutputFolder = "output";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    // Register format-specific repair handlers here. Adding a format requires a
    // registration, not another branch in the worker's processing flow.
    private static readonly IReadOnlyDictionary<string, RepairHandler> RepairHandlers =

         new Dictionary<string, RepairHandler>(StringComparer.Ordinal)
         {
             ["repair.stl"] = new(
                "model/stl",
                (processor, inputPath, sourceFileName, outputPath) =>
                {
                    var report = processor.RepairStl(inputPath, sourceFileName, outputPath);
                    return new RepairArtifact(
                        report.HasCleanedFile,
                        MeshQualityReportFormatter.FormatJson(report));
                }),
             ["repair.3mf"] = new(
                "application/vnd.ms-3mfdocument",
                (processor, inputPath, sourceFileName, outputPath) =>
                {
                    var report = processor.RepairThreeMf(inputPath, sourceFileName, outputPath);
                    return new RepairArtifact(
                        report.Repair.RepairedFileAvailable,
                        MeshQualityReportFormatter.FormatJson(report));
                })
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
        if (!TryGetJobInput(objectKey, out var jobId, out _))
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
            $"{jobsPrefix}{jobId}/{OutputFolder}/failure.json",
            failureJson,
            cancellationToken);
    }

    private bool TryGetJobInput(string objectKey, out string jobId, out string fileName)
    {
        jobId = "";
        fileName = "";
        if (!objectKey.StartsWith(jobsPrefix, StringComparison.Ordinal))
        {
            return false;
        }
        var relativeKey = objectKey[jobsPrefix.Length..];
        var parts = relativeKey.Split('/');
        if (parts.Length != 3 || parts[1] != InputFolder || !IsSupportedInputName(parts[2]))
        {
            return false;
        }
        jobId = parts[0];
        fileName = parts[2];
        return jobId is not "." and not ".." && jobId.Length > 0;
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

        if (!TryGetJobInput(objectKey, out var jobId, out var fileName))
        {
            throw new InvalidDataException(
                $"Object key '{objectKey}' must be a supported job input.");
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

            var isRepair = RepairHandlers.TryGetValue(fileName, out var repairHandler);
            var splitOptions = fileName == "split.stl" ? await ReadSplitOptionsAsync(jobId, cancellationToken) : null;
            var repairOutputPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}-cleaned{Path.GetExtension(fileName)}");
            RepairArtifact? repairArtifact = null;
            MeshQualityReport? report = null;
            if (splitOptions is not null)
            {
                var partAPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}-part-a.stl");
                var partBPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}-part-b.stl");
                var splitReport = processor.SplitStl(temporaryPath, splitOptions.FileName, splitOptions.Axis, splitOptions.PositionPercent, partAPath, partBPath);
                repairArtifact = new RepairArtifact(true, MeshQualityReportFormatter.FormatJson(splitReport));
                await storage.UploadFileAsync(bucketName, $"{jobsPrefix}{jobId}/{OutputFolder}/part-a.stl", partAPath, "model/stl", cancellationToken);
                await storage.UploadFileAsync(bucketName, $"{jobsPrefix}{jobId}/{OutputFolder}/part-b.stl", partBPath, "model/stl", cancellationToken);
                File.Delete(partAPath);
                File.Delete(partBPath);
            }
            else if (isRepair)
            {
                repairArtifact = repairHandler!.Invoke(
                    processor,
                    temporaryPath,
                    fileName,
                    repairOutputPath);
            }
            else
            {
                report = processor.Process(temporaryPath, fileName);
            }
            var reportKey = $"{jobsPrefix}{jobId}/{OutputFolder}/report.json";
            await storage.UploadJsonAsync(
                bucketName,
                reportKey,
                repairArtifact?.ReportJson ?? MeshQualityReportFormatter.FormatJson(report!),
                cancellationToken);
            if (repairArtifact is { HasOutputFile: true } && splitOptions is null)
            {
                await storage.UploadFileAsync(
                    bucketName,
                    $"{jobsPrefix}{jobId}/{OutputFolder}/cleaned{Path.GetExtension(fileName)}",
                    repairOutputPath,
                    repairHandler!.OutputContentType,
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
        return name is "input.stl" or "input.3mf" or "split.stl" || RepairHandlers.ContainsKey(name);
    }

    private async Task<SplitOptions?> ReadSplitOptionsAsync(string jobId, CancellationToken cancellationToken)
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}-metadata.json");
        try
        {
            await using var target = File.Create(path);
            await storage.DownloadAsync(bucketName, $"{jobsPrefix}{jobId}/metadata.json", target, cancellationToken);
            await target.FlushAsync(cancellationToken);
            var metadata = JsonSerializer.Deserialize<SplitMetadata>(await File.ReadAllTextAsync(path, cancellationToken), JsonOptions);
            if (metadata?.Operation != "stl_split" || !Enum.TryParse<Stl.SplitAxis>(metadata.SplitAxis, out var axis))
            {
                return null;
            }
            return new SplitOptions(metadata.FileName ?? "model.stl", axis, metadata.SplitPositionPercent);
        }
        finally { File.Delete(path); }
    }

    private static string DecodeObjectKey(string objectKey)
    {
        return Uri.UnescapeDataString(objectKey.Replace('+', ' '));
    }

    private sealed record RepairHandler(
        string OutputContentType,
        Func<ProcessorApplication, string, string, string, RepairArtifact> Run)
    {
        public RepairArtifact Invoke(
            ProcessorApplication processor,
            string inputPath,
            string sourceFileName,
            string outputPath)
        {
            return Run(processor, inputPath, sourceFileName, outputPath);
        }
    }

    private sealed record RepairArtifact(bool HasOutputFile, string ReportJson);
    private sealed record SplitOptions(string FileName, Stl.SplitAxis Axis, double PositionPercent);
    private sealed class SplitMetadata
    {
        public string? FileName
        {
            get; init;
        }
        public string? Operation
        {
            get; init;
        }
        public string? SplitAxis
        {
            get; init;
        }
        public double SplitPositionPercent
        {
            get; init;
        }
    }
}
