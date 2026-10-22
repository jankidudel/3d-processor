using Processor.Models;
using Processor.Reporting;
using Processor.Stl;
using Xunit.Abstractions;

namespace Processor.Tests.Performance;

public class RepeatedModelMemoryTests(ITestOutputHelper output)
{
    [Fact]
    public void Process_281540TrianglesTwice_KeepsAllocationBoundedAndResultsStable()
    {
        var path = Path.Combine(Path.GetTempPath(), $"memory-regression-{Guid.NewGuid():N}.stl");
        try
        {
            WriteTetrahedrons(path, 70_385);
            string? firstJson = null;
            for (var run = 0; run < 2; run++)
            {
                // Per-thread allocations are deterministic enough for a broad
                // regression budget; RSS varies with the test host and platform.
                var before = GC.GetAllocatedBytesForCurrentThread();
                var report = new ProcessorApplication().Process(path);
                var json = MeshQualityReportFormatter.FormatJson(report);
                var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
                output.WriteLine($"Run {run + 1}: allocated {allocated / 1048576d:F1} MiB");

                Assert.True(allocated < 128L * 1024 * 1024,
                    $"Analysis allocated {allocated / 1048576d:F1} MiB; budget is 128 MiB.");
                Assert.Equal(281_540, report.TriangleCount);
                Assert.True(report.Validation.IsValid);
                Assert.Equal(70_385, report.Topology.ConnectedComponentCount);
                Assert.Equal(0, report.Topology.OpenEdgeCount);
                Assert.Equal(0, report.Topology.NonManifoldEdgeCount);
                Assert.Equal(0, report.IssueGeometry!.OpenEdges!.TotalCount);
                Assert.Equal(0, report.IssueGeometry.NonManifoldEdges!.TotalCount);
                if (firstJson is not null)
                {
                    Assert.Equal(firstJson, json);
                }
                firstJson = json;
            }
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static void WriteTetrahedrons(string path, int count)
    {
        using var stream = File.Create(path);
        using var writer = new BinaryWriter(stream);
        writer.Write(new byte[80]);
        writer.Write(checked((uint)count * 4));
        for (var i = 0; i < count; i++)
        {
            var a = new Vector3(i * 2, 0, 0);
            var b = new Vector3(a.X + 1, 0, 0);
            var c = new Vector3(a.X, 1, 0);
            var d = new Vector3(a.X, 0, 1);
            WriteTriangle(writer, a, c, b);
            WriteTriangle(writer, a, b, d);
            WriteTriangle(writer, a, d, c);
            WriteTriangle(writer, b, c, d);
        }
    }

    private static void WriteTriangle(BinaryWriter writer, Vector3 a, Vector3 b, Vector3 c)
    {
        WriteVector(writer, default);
        WriteVector(writer, a);
        WriteVector(writer, b);
        WriteVector(writer, c);
        writer.Write((ushort)0);
    }

    private static void WriteVector(BinaryWriter writer, Vector3 value)
    {
        writer.Write(value.X);
        writer.Write(value.Y);
        writer.Write(value.Z);
    }
}
