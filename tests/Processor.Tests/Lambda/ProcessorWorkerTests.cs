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
            "jobs/job-123/input.stl");

        var response = await worker.ProcessAsync(sqsEvent);

        Assert.Empty(response.BatchItemFailures);
        var upload = Assert.Single(storage.Uploads);
        Assert.Equal("storage-bucket", upload.BucketName);
        Assert.Equal("jobs/job-123/report.json", upload.ObjectKey);
        using var report = JsonDocument.Parse(upload.Json);
        Assert.Equal("input.stl", report.RootElement.GetProperty("fileName").GetString());
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
                CreateMessage("message-1", "storage-bucket", "jobs/job-456/input.stl"),
                CreateMessage("message-2", "storage-bucket", "jobs/job-456/input.stl")
            ]
        };

        var response = await worker.ProcessAsync(sqsEvent);

        Assert.Empty(response.BatchItemFailures);
        Assert.Equal(2, storage.Uploads.Count);
        Assert.All(storage.Uploads, upload =>
            Assert.Equal("jobs/job-456/report.json", upload.ObjectKey));
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
    public async Task ProcessAsync_S3NotificationTestEvent_IsAcknowledgedWithoutRetrying()
    {
        var path = Path.GetTempFileName();
        try
        {
            var storage = new FakeObjectStorage(path);
            var worker = CreateWorker(storage);
            var sqsEvent = new SQSEvent
            {
                Records =
                [
                    new SQSEvent.SQSMessage
                    {
                        MessageId = "s3-test-event",
                        Body = "{\"Service\":\"Amazon S3\",\"Event\":\"s3:TestEvent\"}"
                    }
                ]
            };

            var response = await worker.ProcessAsync(sqsEvent);

            Assert.Empty(response.BatchItemFailures);
            Assert.Empty(storage.Uploads);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ProcessAsync_UnsupportedFile_ReturnsPartialBatchFailure()
    {
        var storage = new FakeObjectStorage(GetFixturePath("valid", "rotor.stl"));
        var worker = CreateWorker(storage);
        var sqsEvent = CreateSqsEvent(
            "message-1",
            "storage-bucket",
            "jobs/job-123/model.obj");

        var response = await worker.ProcessAsync(sqsEvent);

        Assert.Single(response.BatchItemFailures);
        Assert.Empty(storage.Uploads);
    }

    [Fact]
    public async Task ProcessAsync_MalformedStl_WritesFailureResultWithoutRetrying()
    {
        var storage = new FakeObjectStorage(GetFixturePath("corrupt", "rotor-truncated.stl"));
        var worker = CreateWorker(storage);
        var sqsEvent = CreateSqsEvent(
            "message-1",
            "storage-bucket",
            "jobs/job-123/input.stl");
        var response = await worker.ProcessAsync(sqsEvent);

        Assert.Empty(response.BatchItemFailures);
        var failure = Assert.Single(storage.Uploads);
        Assert.Equal("jobs/job-123/failure.json", failure.ObjectKey);
        using var json = JsonDocument.Parse(failure.Json);
        Assert.Equal("processing_failed", json.RootElement.GetProperty("error").GetProperty("code").GetString());
        Assert.Equal(
            "This file is not a valid STL file or is incomplete.",
            json.RootElement.GetProperty("error").GetProperty("message").GetString());
    }

    [Fact]
    public async Task ProcessAsync_InvalidThreeMf_WritesControlledFailureWithoutRetrying()
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.3mf");
        await File.WriteAllTextAsync(path, "not a ZIP package");
        try
        {
            var storage = new FakeObjectStorage(path);
            var worker = CreateWorker(storage);
            var response = await worker.ProcessAsync(CreateSqsEvent(
                "message-3mf", "storage-bucket", "jobs/job-3mf/input.3mf"));

            Assert.Empty(response.BatchItemFailures);
            var failure = Assert.Single(storage.Uploads);
            using var json = JsonDocument.Parse(failure.Json);
            Assert.Equal("This 3MF file could not be analysed safely.", json.RootElement.GetProperty("error").GetProperty("message").GetString());
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ProcessAsync_UnsupportedThreeMfFeature_WritesSpecificSafeFailure()
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.3mf");
        await using (var stream = File.Create(path))
        using (var archive = new System.IO.Compression.ZipArchive(stream, System.IO.Compression.ZipArchiveMode.Create))
        {
            WriteEntry(archive, "_rels/.rels", """<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rel" Type="http://schemas.microsoft.com/3dmanufacturing/2013/01/3dmodel" Target="3D/model.model"/></Relationships>""");
            WriteEntry(archive, "3D/model.model", """<model requiredextensions="p" xmlns="http://schemas.microsoft.com/3dmanufacturing/core/2015/02"><resources/><build/></model>""");
        }
        try
        {
            var storage = new FakeObjectStorage(path);
            var response = await CreateWorker(storage).ProcessAsync(CreateSqsEvent("message-feature", "storage-bucket", "jobs/job-feature/input.3mf"));
            Assert.Empty(response.BatchItemFailures);
            using var json = JsonDocument.Parse(Assert.Single(storage.Uploads).Json);
            Assert.Equal("This 3MF file uses a feature that InspectMesh does not support yet.", json.RootElement.GetProperty("error").GetProperty("message").GetString());
        }
        finally { File.Delete(path); }
    }

    private static void WriteEntry(System.IO.Compression.ZipArchive archive, string name, string content)
    {
        using var writer = new StreamWriter(archive.CreateEntry(name).Open());
        writer.Write(content);
    }

    [Fact]
    public async Task ProcessAsync_ValidAsciiStl_WritesNormalReport()
    {
        var storage = new FakeObjectStorage(
            GetFixturePath("ascii", "validation-success-closed-tetrahedron.stl"));
        var worker = CreateWorker(storage);
        var sqsEvent = CreateSqsEvent("message-1", "storage-bucket", "jobs/job-ascii/input.stl");

        var response = await worker.ProcessAsync(sqsEvent);

        Assert.Empty(response.BatchItemFailures);
        var upload = Assert.Single(storage.Uploads);
        Assert.Equal("jobs/job-ascii/report.json", upload.ObjectKey);
        using var report = JsonDocument.Parse(upload.Json);
        Assert.Equal(4, report.RootElement.GetProperty("triangleCount").GetInt32());
        Assert.True(report.RootElement.GetProperty("validation").GetProperty("isValid").GetBoolean());
    }

    [Fact]
    public async Task ProcessAsync_OversizedFile_ReturnsPartialBatchFailureWithoutDownloading()
    {
        var storage = new FakeObjectStorage(GetFixturePath("valid", "rotor.stl"));
        var worker = CreateWorker(storage);
        var sqsEvent = CreateSqsEvent(
            "message-1",
            "storage-bucket",
            "jobs/job-123/input.stl",
            ProcessorWorker.MaxUploadSizeBytes + 1);

        var response = await worker.ProcessAsync(sqsEvent);

        Assert.Single(response.BatchItemFailures);
        Assert.Equal(0, storage.DownloadCount);
        Assert.Empty(storage.Uploads);
    }

    [Fact]
    public async Task ProcessAsync_ObjectReplacedWithOversizedFile_DoesNotDownloadCurrentObject()
    {
        var storage = new FakeObjectStorage(GetFixturePath("valid", "rotor.stl"))
        {
            ObjectSize = ProcessorWorker.MaxUploadSizeBytes + 1
        };
        var worker = CreateWorker(storage);
        var sqsEvent = CreateSqsEvent(
            "message-1",
            "storage-bucket",
            "jobs/job-123/input.stl",
            117884);

        var response = await worker.ProcessAsync(sqsEvent);

        Assert.Single(response.BatchItemFailures);
        Assert.Equal(0, storage.DownloadCount);
        Assert.Empty(storage.Uploads);
    }

    private static ProcessorWorker CreateWorker(IObjectStorage storage)
    {
        return new ProcessorWorker(
            storage,
            new ProcessorApplication(),
            "storage-bucket",
            "jobs/");
    }

    private static SQSEvent CreateSqsEvent(
        string messageId,
        string bucketName,
        string objectKey,
        long objectSize = 0)
    {
        return new SQSEvent
        {
            Records = [CreateMessage(messageId, bucketName, objectKey, objectSize)]
        };
    }

    private static SQSEvent.SQSMessage CreateMessage(
        string messageId,
        string bucketName,
        string objectKey,
        long objectSize = 0)
    {
        return new SQSEvent.SQSMessage
        {
            MessageId = messageId,
            Attributes = new Dictionary<string, string>(),
            Body = JsonSerializer.Serialize(new
            {
                Records = new[]
                {
                    new
                    {
                        s3 = new
                        {
                            bucket = new { name = bucketName },
                            @object = new { key = objectKey, size = objectSize }
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
        return Path.Combine(directory.FullName, "tests", "TestData", "stl", directoryName, fileName);
    }

    private sealed class FakeObjectStorage(string sourceFilePath) : IObjectStorage
    {
        public List<Upload> Uploads { get; } = [];
        public List<FileUpload> FileUploads { get; } = [];
        public int DownloadCount { get; private set; }
        public long ObjectSize { get; init; } = new FileInfo(sourceFilePath).Length;

        public Task<long> GetSizeAsync(
            string bucketName,
            string objectKey,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(ObjectSize);
        }

        public async Task DownloadAsync(
            string bucketName,
            string objectKey,
            Stream destination,
            CancellationToken cancellationToken = default)
        {
            DownloadCount++;
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

        public Task UploadFileAsync(
            string bucketName,
            string objectKey,
            string filePath,
            string contentType,
            CancellationToken cancellationToken = default)
        {
            FileUploads.Add(new FileUpload(bucketName, objectKey, contentType, File.ReadAllBytes(filePath)));
            return Task.CompletedTask;
        }
    }

    private sealed record Upload(string BucketName, string ObjectKey, string Json);
    private sealed record FileUpload(string BucketName, string ObjectKey, string ContentType, byte[] Contents);
}
