using Processor.Validation;

namespace Processor.Reporting;

public sealed class StlQualityReport
{
    public string ReportVersion { get; init; } = "1.0";
    public string FileName { get; init; } = "";
    public long FileSizeBytes { get; init; }
    public int TriangleCount { get; init; }
    public MeshBoundingBox BoundingBox { get; init; } = new();
    public MeshDimensions Dimensions { get; init; } = new();
    public double SurfaceArea { get; init; }
    public double Volume { get; init; }
    public ValidationResult Validation { get; init; } = new();
}
