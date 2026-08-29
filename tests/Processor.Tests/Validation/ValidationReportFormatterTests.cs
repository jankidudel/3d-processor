using System.Text.Json;
using Processor.Reporting;
using Processor.Validation;

namespace Processor.Tests.Validation;

public class ValidationReportFormatterTests
{
    [Fact]
    public void FormatText_ValidResult_ContainsQualityCountsAndOkStatus()
    {
        var reportModel = CreateReport(1246, new ValidationResult());

        var report = MeshQualityReportFormatter.FormatText(reportModel);

        Assert.Contains("STL Quality Report", report);
        Assert.Contains("Triangles:                1246", report);
        Assert.Contains("Degenerate triangles:     0", report);
        Assert.Contains("Validation: OK", report);
    }

    [Fact]
    public void FormatJson_ResultWithIssues_ContainsStructuredCounts()
    {
        var validation = new ValidationResult
        {
            Issues =
            [
                new ValidationIssue(ValidationIssueCodes.OpenEdge, "First open edge."),
                new ValidationIssue(ValidationIssueCodes.OpenEdge, "Two more open edges.", 2),
                new ValidationIssue(ValidationIssueCodes.DuplicateTriangle, "Duplicate triangle.")
            ]
        };

        var report = MeshQualityReportFormatter.FormatJson(CreateReport(12, validation));
        using var document = JsonDocument.Parse(report);
        var root = document.RootElement;

        Assert.Equal("1.0", root.GetProperty("reportVersion").GetString());
        Assert.Equal(12, root.GetProperty("triangleCount").GetInt32());
        Assert.Equal(0, root.GetProperty("topology").GetProperty("connectedComponentCount").GetInt32());
        var validationJson = root.GetProperty("validation");
        Assert.False(validationJson.GetProperty("isValid").GetBoolean());
        Assert.Equal(2, validationJson.GetProperty("issues")[1].GetProperty("count").GetInt32());
    }

    [Fact]
    public void FormatJson_ThreeMfReport_ContainsOptionalFormatFields()
    {
        var report = new MeshQualityReport
        {
            FileName = "model.3mf",
            FileSizeBytes = 100,
            TriangleCount = 12,
            Validation = new ValidationResult(),
            FileFormat = "3mf",
            SourceUnit = "millimeter",
            ModelObjectCount = 2,
            BuildItemCount = 3
        };

        using var document = JsonDocument.Parse(MeshQualityReportFormatter.FormatJson(report));
        var root = document.RootElement;

        Assert.Equal("3mf", root.GetProperty("fileFormat").GetString());
        Assert.Equal("millimeter", root.GetProperty("sourceUnit").GetString());
        Assert.Equal(2, root.GetProperty("modelObjectCount").GetInt32());
        Assert.Equal(3, root.GetProperty("buildItemCount").GetInt32());
    }

    [Fact]
    public void FormatJson_StlReport_ExplicitlyIdentifiesStlFormat()
    {
        using var document = JsonDocument.Parse(MeshQualityReportFormatter.FormatJson(CreateReport(1, new ValidationResult())));
        Assert.Equal("stl", document.RootElement.GetProperty("fileFormat").GetString());
    }

    private static MeshQualityReport CreateReport(int triangleCount, ValidationResult validation)
    {
        return new MeshQualityReport
        {
            FileName = "model.stl",
            FileSizeBytes = 100,
            TriangleCount = triangleCount,
            Validation = validation
        };
    }
}
