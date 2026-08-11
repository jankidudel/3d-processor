using System.Text.Json;
using Processor.Validation;

namespace Processor.Tests.Validation;

public class ValidationReportFormatterTests
{
    [Fact]
    public void FormatText_ValidResult_ContainsQualityCountsAndOkStatus()
    {
        var result = new ValidationResult { TriangleCount = 1246 };

        var report = ValidationReportFormatter.FormatText(result);

        Assert.Contains("STL Quality Report", report);
        Assert.Contains("Triangles:                1246", report);
        Assert.Contains("Degenerate triangles:     0", report);
        Assert.Contains("Validation: OK", report);
    }

    [Fact]
    public void FormatJson_ResultWithIssues_ContainsStructuredCounts()
    {
        var result = new ValidationResult
        {
            TriangleCount = 12,
            Issues =
            [
                new ValidationIssue(ValidationIssueCodes.OpenEdge, "First open edge."),
                new ValidationIssue(ValidationIssueCodes.OpenEdge, "Two more open edges.", 2),
                new ValidationIssue(ValidationIssueCodes.DuplicateTriangle, "Duplicate triangle.")
            ]
        };

        var report = ValidationReportFormatter.FormatJson(result);
        using var document = JsonDocument.Parse(report);
        var root = document.RootElement;

        Assert.False(root.GetProperty("valid").GetBoolean());
        Assert.Equal(12, root.GetProperty("triangleCount").GetInt32());
        Assert.Equal(3, root.GetProperty("issues").GetProperty("openEdges").GetInt32());
        Assert.Equal(1, root.GetProperty("issues").GetProperty("duplicateTriangles").GetInt32());
        Assert.Equal(0, root.GetProperty("issues").GetProperty("invalidCoordinates").GetInt32());
    }
}
