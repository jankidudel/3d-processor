using System.Text.Json;

namespace Processor.Tests.Integration;

public class ProcessorApplicationIntegrationTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public void Run_ValidStlAsJson_WritesStableReportAndReturnsZero()
    {
        var output = new StringWriter();
        var error = new StringWriter();
        var filePath = GetFixturePath("valid", "rotor.stl");

        var exitCode = new ProcessorApplication().Run([filePath, "--json"], output, error);
        using var document = JsonDocument.Parse(output.ToString());
        var report = document.RootElement;

        Assert.Equal(0, exitCode);
        Assert.Equal("", error.ToString());
        Assert.Equal("1.0", report.GetProperty("reportVersion").GetString());
        Assert.Equal("rotor.stl", report.GetProperty("fileName").GetString());
        Assert.Equal(117884, report.GetProperty("fileSizeBytes").GetInt64());
        Assert.Equal(2356, report.GetProperty("triangleCount").GetInt32());
        Assert.Equal(46.5, report.GetProperty("boundingBox").GetProperty("minX").GetDouble());
        Assert.Equal(73.5, report.GetProperty("boundingBox").GetProperty("maxX").GetDouble());
        Assert.Equal(27, report.GetProperty("dimensions").GetProperty("width").GetDouble());
        Assert.Equal(23.4, report.GetProperty("dimensions").GetProperty("height").GetDouble(), 5);
        Assert.Equal(27, report.GetProperty("dimensions").GetProperty("depth").GetDouble(), 5);
        Assert.True(report.GetProperty("surfaceArea").GetDouble() > 0);
        Assert.True(report.GetProperty("volume").GetDouble() > 0);
        Assert.True(report.GetProperty("validation").GetProperty("isValid").GetBoolean());
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void Run_InvalidStlAsJson_WritesValidJsonAndReturnsTwo()
    {
        var output = new StringWriter();
        var error = new StringWriter();
        var filePath = GetFixturePath("degenerate", "rotor-degenerate.stl");

        var exitCode = new ProcessorApplication().Run([filePath, "--json"], output, error);
        using var document = JsonDocument.Parse(output.ToString());

        Assert.Equal(2, exitCode);
        Assert.Equal("", error.ToString());
        Assert.False(document.RootElement
            .GetProperty("validation")
            .GetProperty("isValid")
            .GetBoolean());
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void Run_TruncatedBinaryStl_WritesOnlyErrorAndReturnsOne()
    {
        var output = new StringWriter();
        var error = new StringWriter();
        var filePath = GetFixturePath("corrupt", "rotor-truncated.stl");

        var exitCode = new ProcessorApplication().Run([filePath, "--json"], output, error);

        Assert.Equal(1, exitCode);
        Assert.Equal("", output.ToString());
        Assert.Contains("Processing failed: Invalid binary STL.", error.ToString());
    }

    private static string GetFixturePath(string directoryName, string fileName)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "3d-processor.slnx")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return Path.Combine(directory.FullName, "tests", "TestData", "stl", directoryName, fileName);
    }
}
