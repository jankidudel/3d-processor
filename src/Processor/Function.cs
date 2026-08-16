using Amazon.Lambda.Core;
using Amazon.Lambda.Serialization.SystemTextJson;
using Amazon.Lambda.SQSEvents;
using Amazon.S3;
using Processor.Lambda;

[assembly: LambdaSerializer(typeof(DefaultLambdaJsonSerializer))]

namespace Processor;

public sealed class Function
{
    private readonly ProcessorWorker _worker;

    public Function()
        : this(CreateWorker())
    {
    }

    internal Function(ProcessorWorker worker)
    {
        _worker = worker;
    }

    public Task<SQSBatchResponse> FunctionHandler(SQSEvent sqsEvent, ILambdaContext context)
    {
        return _worker.ProcessAsync(sqsEvent, context.Logger.LogLine);
    }

    private static ProcessorWorker CreateWorker()
    {
        return new ProcessorWorker(
            new S3ObjectStorage(new AmazonS3Client()),
            new ProcessorApplication(),
            RequiredEnvironmentVariable("STORAGE_BUCKET_NAME"),
            RequiredEnvironmentVariable("JOBS_PREFIX"));
    }

    private static string RequiredEnvironmentVariable(string name)
    {
        return Environment.GetEnvironmentVariable(name) is { Length: > 0 } value
            ? value
            : throw new InvalidOperationException($"Environment variable {name} is required.");
    }
}
