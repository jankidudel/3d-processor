using Processor.Validation;
using Processor.Topology;

namespace Processor.Reporting;

public sealed class MeshQualityReport
{
    public string ReportVersion { get; init; } = "1.0";
    public string FileName { get; init; } = "";
    public string FileFormat { get; init; } = "stl";
    public string? SourceUnit { get; init; }
    public int? ModelObjectCount { get; init; }
    public int? BuildItemCount { get; init; }
    public long FileSizeBytes { get; init; }
    public int TriangleCount { get; init; }
    public MeshBoundingBox BoundingBox { get; init; } = new();
    public MeshDimensions Dimensions { get; init; } = new();
    public double SurfaceArea { get; init; }
    public double Volume { get; init; }
    public MeshTopology Topology { get; init; } = new();
    public ValidationResult Validation { get; init; } = new();
    public MeshIssueHighlights? IssueHighlights { get; init; }
}
