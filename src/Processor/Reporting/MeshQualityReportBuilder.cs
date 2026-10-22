using Processor.Models;
using Processor.Validation;

namespace Processor.Reporting;

public sealed class MeshQualityReportBuilder
{
    public MeshQualityReport Build(
        string filePath,
        Mesh mesh,
        ValidationResult validation,
        string? sourceFileName = null,
        string fileFormat = "stl",
        string? sourceUnit = null,
        int? modelObjectCount = null,
        int? buildItemCount = null,
        bool includeIssueHighlights = true,
        IReadOnlyList<Mesh>? highlightMeshes = null)
    {
        var geometry = validation.Geometry ?? MeshGeometryAccumulator.Calculate(mesh);
        var meshesForHighlights = highlightMeshes ?? [mesh];
        var issueAnalysis = includeIssueHighlights
            ? validation.IssueAnalysis ?? MeshIssueHighlightSampler.Analyze(meshesForHighlights)
            : null;

        return new MeshQualityReport
        {
            FileName = sourceFileName ?? Path.GetFileName(filePath),
            FileFormat = fileFormat,
            SourceUnit = sourceUnit,
            ModelObjectCount = modelObjectCount,
            BuildItemCount = buildItemCount,
            FileSizeBytes = new FileInfo(filePath).Length,
            TriangleCount = mesh.Triangles.Count,
            BoundingBox = geometry.BoundingBox,
            Dimensions = new MeshDimensions
            {
                Width = geometry.BoundingBox.MaxX - geometry.BoundingBox.MinX,
                Height = geometry.BoundingBox.MaxY - geometry.BoundingBox.MinY,
                Depth = geometry.BoundingBox.MaxZ - geometry.BoundingBox.MinZ
            },
            SurfaceArea = geometry.SurfaceArea,
            Volume = validation.IsValid &&
                validation.Topology.ConnectedComponentCount == 1 &&
                validation.Topology.OpenEdgeCount == 0 &&
                validation.Topology.NonManifoldEdgeCount == 0 &&
                !validation.Topology.HasInconsistentOrientation
                    ? Math.Abs(geometry.SignedVolume)
                    : null,
            Topology = validation.Topology,
            Validation = validation,
            IssueHighlights = issueAnalysis?.Highlights,
            IssueGeometry = issueAnalysis?.Geometry
        };
    }
}
