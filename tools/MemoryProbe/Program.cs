using System.Diagnostics;
using Processor;
using Processor.Reporting;

if (args.Length != 1)
{
    Console.Error.WriteLine("Usage: MemoryProbe <file.stl|file.3mf>");
    return 1;
}

using var process = Process.GetCurrentProcess();
using var stop = new ManualResetEventSlim();
long sampledPeak = 0;
var sampler = new Thread(() =>
{
    using var sampledProcess = Process.GetCurrentProcess();
    while (!stop.IsSet)
    {
        sampledProcess.Refresh();
        Interlocked.Exchange(ref sampledPeak, Math.Max(sampledPeak, sampledProcess.WorkingSet64));
        stop.Wait(2);
    }
});
sampler.Start();

try
{
    // No forced collection between calls: exercise a reused worker process.
    for (var run = 1; run <= 2; run++)
    {
        var allocatedBefore = GC.GetTotalAllocatedBytes(true);
        var stopwatch = Stopwatch.StartNew();
        var report = new ProcessorApplication().Process(args[0]);
        var json = MeshQualityReportFormatter.FormatJson(report);
        stopwatch.Stop();
        process.Refresh();
        Console.WriteLine(
            $"Run={run}; triangles={report.TriangleCount}; " +
            $"elapsed={stopwatch.Elapsed.TotalSeconds:F3}s; " +
            $"allocated={(GC.GetTotalAllocatedBytes(true) - allocatedBefore) / 1048576d:F1} MiB; " +
            $"workingSet={process.WorkingSet64 / 1048576d:F1} MiB; " +
            $"sampledPeak={Interlocked.Read(ref sampledPeak) / 1048576d:F1} MiB; " +
            $"jsonBytes={System.Text.Encoding.UTF8.GetByteCount(json)}");
        Console.WriteLine(MeshQualityReportFormatter.FormatText(report));
        Console.WriteLine(
            $"Overlay: open={report.IssueGeometry?.OpenEdges?.TotalCount}; " +
            $"nonManifold={report.IssueGeometry?.NonManifoldEdges?.TotalCount}");
    }
}
finally
{
    stop.Set();
    sampler.Join();
}
return 0;
