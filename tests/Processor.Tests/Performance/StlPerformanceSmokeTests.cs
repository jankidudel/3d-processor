using System.Diagnostics;
using Processor.Reporting;
using Xunit.Abstractions;

namespace Processor.Tests.Performance;

public class StlPerformanceSmokeTests(ITestOutputHelper output)
{
    [Fact]
    [Trait("Category", "Performance")]
    public void Process_RepresentativeFileSizes_ReportsTimeMemoryAndTriangleCount()
    {
        if (Environment.GetEnvironmentVariable("RUN_PERFORMANCE_SMOKE_TESTS") != "1")
        {
            output.WriteLine("Set RUN_PERFORMANCE_SMOKE_TESTS=1 to run the opt-in performance smoke test.");
            return;
        }

        foreach (var targetSizeMb in new[] { 1, 20, 100 })
        {
            var filePath = Path.Combine(Path.GetTempPath(), $"processor-smoke-{targetSizeMb}mb.stl");

            try
            {
                var expectedTriangleCount = WriteTetrahedrons(filePath, targetSizeMb);
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();

                var process = Process.GetCurrentProcess();
                var initialWorkingSet = process.WorkingSet64;
                var stopwatch = Stopwatch.StartNew();

                MeshQualityReport report = new ProcessorApplication().Process(filePath);

                stopwatch.Stop();
                process.Refresh();
                var workingSetIncrease = Math.Max(0, process.WorkingSet64 - initialWorkingSet);
                var peakOrCurrentWorkingSet = process.PeakWorkingSet64 > 0
                    ? process.PeakWorkingSet64
                    : process.WorkingSet64;
                var managedHeapSize = GC.GetGCMemoryInfo().HeapSizeBytes;
                var liveManagedMemory = GC.GetTotalMemory(false);

                output.WriteLine(
                    $"Target={targetSizeMb} MB; " +
                    $"Triangles={report.TriangleCount}; " +
                    $"Elapsed={stopwatch.Elapsed}; " +
                    $"Working-set increase={workingSetIncrease / 1024d / 1024d:F1} MB; " +
                    $"Peak/current working set={peakOrCurrentWorkingSet / 1024d / 1024d:F1} MB; " +
                    $"Managed heap={managedHeapSize / 1024d / 1024d:F1} MB; " +
                    $"Live managed memory={liveManagedMemory / 1024d / 1024d:F1} MB");

                Assert.Equal(expectedTriangleCount, report.TriangleCount);
                Assert.True(report.Validation.IsValid);
            }
            finally
            {
                File.Delete(filePath);
            }
        }
    }

    private static int WriteTetrahedrons(string filePath, int targetSizeMb)
    {
        const int headerSize = 84;
        const int triangleSize = 50;
        const int trianglesPerTetrahedron = 4;
        var targetBytes = targetSizeMb * 1024L * 1024L;
        var triangleCount = (int)((targetBytes - headerSize) / triangleSize);
        triangleCount -= triangleCount % trianglesPerTetrahedron;

        using var stream = File.Create(filePath);
        using var writer = new BinaryWriter(stream);
        writer.Write(new byte[80]);
        writer.Write((uint)triangleCount);

        for (var tetrahedronIndex = 0;
             tetrahedronIndex < triangleCount / trianglesPerTetrahedron;
             tetrahedronIndex++)
        {
            var offset = tetrahedronIndex * 2f;
            var a = (X: offset, Y: 0f, Z: 0f);
            var b = (X: offset + 1f, Y: 0f, Z: 0f);
            var c = (X: offset, Y: 1f, Z: 0f);
            var d = (X: offset, Y: 0f, Z: 1f);

            WriteTriangle(writer, a, c, b);
            WriteTriangle(writer, a, b, d);
            WriteTriangle(writer, a, d, c);
            WriteTriangle(writer, b, c, d);
        }

        return triangleCount;
    }

    private static void WriteTriangle(
        BinaryWriter writer,
        (float X, float Y, float Z) a,
        (float X, float Y, float Z) b,
        (float X, float Y, float Z) c)
    {
        WriteVector(writer, (0, 0, 0));
        WriteVector(writer, a);
        WriteVector(writer, b);
        WriteVector(writer, c);
        writer.Write((ushort)0);
    }

    private static void WriteVector(BinaryWriter writer, (float X, float Y, float Z) vector)
    {
        writer.Write(vector.X);
        writer.Write(vector.Y);
        writer.Write(vector.Z);
    }
}
