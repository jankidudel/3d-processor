using System.Text.Json;
using Amazon.Lambda.SQSEvents;
using Processor.Lambda;

namespace Processor.Tests.Lambda;

public class ProcessorWorkerTests
{
    [Fact]
    public async Task ProcessAsync_ValidS3Event_WritesDeterministicJsonReport()
    {
        var storage = new FakeObjectStorage(GetFixturePath("valid", "rotor.stl"));
        var worker = CreateWorker(storage);
        var sqsEvent = CreateSqsEvent(
            "message-1",
            "storage-bucket",
            "uploads/job-123/my+model.stl");

        var response = await worker.ProcessAsync(sqsEvent);

        Assert.Empty(response.BatchItemFailures);
        var upload = Assert.Single(storage.Uploads);
        Assert.Equal("storage-bucket", upload.BucketName);
        Assert.Equal("reports/job-123/report.json", upload.ObjectKey);
        using var report = JsonDocument.Parse(upload.Json);
        Assert.Equal("my model.stl", report.RootElement.GetProperty("fileName").GetString());
        Assert.Equal(2356, report.RootElement.GetProperty("triangleCount").GetInt32());
    }

    [Fact]
    public async Task ProcessAsync_DuplicateDelivery_OverwritesTheSameReportKey()
    {
        var storage = new FakeObjectStorage(GetFixturePath("valid", "releaser.stl"));
        var worker = CreateWorker(storage);
        var sqsEvent = new SQSEvent
        {
            Records =
            [
                CreateMessage("message-1", "storage-bucket", "uploads/job-456/model.stl"),
                CreateMessage("message-2", "storage-bucket", "uploads/job-456/model.stl")
            ]
        };

        var response = await worker.ProcessAsync(sqsEvent);

        Assert.Empty(response.BatchItemFailures);
        Assert.Equal(2, storage.Uploads.Count);
        Assert.All(storage.Uploads, upload =>
            Assert.Equal("reports/job-456/report.json", upload.ObjectKey));
    }

    [Fact]
    public async Task ProcessAsync_InvalidMessage_ReturnsPartialBatchFailure()
    {
        var storage = new FakeObjectStorage(GetFixturePath("valid", "rotor.stl"));
        var worker = CreateWorker(storage);
        var sqsEvent = new SQSEvent
        {
            Records = [new SQSEvent.SQSMessage { MessageId = "bad-message", Body = "not-json" }]
        };

        var response = await worker.ProcessAsync(sqsEvent);

        var failure = Assert.Single(response.BatchItemFailures);
        Assert.Equal("bad-message", failure.ItemIdentifier);
        Assert.Empty(storage.Uploads);
    }

    [Fact]
    public async Task ProcessAsync_UnsupportedFile_ReturnsPartialBatchFailure()
    {
        var storage = new FakeObjectStorage(GetFixturePath("valid", "rotor.stl"));
        var worker = CreateWorker(storage);
        var sqsEvent = CreateSqsEvent(
            "message-1",
            "storage-bucket",
            "uploads/job-123/model.obj");

        var response = await worker.ProcessAsync(sqsEvent);

        Assert.Single(response.BatchItemFailures);
        Assert.Empty(storage.Uploads);
    }

    private static ProcessorWorker CreateWorker(IObjectStorage storage)
    {
        return new ProcessorWorker(
            storage,
            new ProcessorApplication(),
            "storage-bucket",
            "uploads/",
            "reports/");
    }

    private static SQSEvent CreateSqsEvent(
        string messageId,
        string bucketName,
        string objectKey)
    {
        return new SQSEvent
        {
            Records = [CreateMessage(messageId, bucketName, objectKey)]
        };
    }

    private static SQSEvent.SQSMessage CreateMessage(
        string messageId,
        string bucketName,
        string objectKey)
    {
        return new SQSEvent.SQSMessage
        {
            MessageId = messageId,
            Body = JsonSerializer.Serialize(new
            {
                Records = new[]
                {
                    new
                    {
                        s3 = new
                        {
                            bucket = new { name = bucketName },
                            @object = new { key = objectKey }
                        }
                    }
                }
            })
        };
    }

    private static string GetFixturePath(string directoryName, string fileName)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null &&
               !File.Exists(Path.Combine(directory.FullName, "3d-processor.slnx")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return Path.Combine(directory.FullName, "data", directoryName, fileName);
    }

    private sealed class FakeObjectStorage(string sourceFilePath) : IObjectStorage
    {
        public List<Upload> Uploads { get; } = [];

        public async Task DownloadAsync(
            string bucketName,
            string objectKey,
            Stream destination,
            CancellationToken cancellationToken = default)
        {
            await using var source = File.OpenRead(sourceFilePath);
            await source.CopyToAsync(destination, cancellationToken);
        }

        public Task UploadJsonAsync(
            string bucketName,
            string objectKey,
            string json,
            CancellationToken cancellationToken = default)
        {
            Uploads.Add(new Upload(bucketName, objectKey, json));
            return Task.CompletedTask;
        }
    }

    private sealed record Upload(string BucketName, string ObjectKey, string Json);
}
