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
        var geometry = CalculateGeometry(mesh);
        var meshesForHighlights = highlightMeshes ?? [mesh];
        var issueAnalysis = includeIssueHighlights
            ? MeshIssueHighlightSampler.Analyze(meshesForHighlights)
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

    private static GeometryMeasurements CalculateGeometry(Mesh mesh)
    {
        var bounds = new BoundsAccumulator();
        double surfaceArea = 0;
        double signedVolume = 0;

        foreach (var triangle in mesh.Triangles)
        {
            bounds.Include(triangle.A);
            bounds.Include(triangle.B);
            bounds.Include(triangle.C);

            if (!IsFinite(triangle.A) || !IsFinite(triangle.B) || !IsFinite(triangle.C))
            {
                continue;
            }

            var abX = (double)triangle.B.X - triangle.A.X;
            var abY = (double)triangle.B.Y - triangle.A.Y;
            var abZ = (double)triangle.B.Z - triangle.A.Z;
            var acX = (double)triangle.C.X - triangle.A.X;
            var acY = (double)triangle.C.Y - triangle.A.Y;
            var acZ = (double)triangle.C.Z - triangle.A.Z;
            var crossX = abY * acZ - abZ * acY;
            var crossY = abZ * acX - abX * acZ;
            var crossZ = abX * acY - abY * acX;

            surfaceArea += 0.5 * Math.Sqrt(
                crossX * crossX + crossY * crossY + crossZ * crossZ);

            signedVolume += (
                (double)triangle.A.X * (triangle.B.Y * triangle.C.Z - triangle.B.Z * triangle.C.Y) -
                (double)triangle.A.Y * (triangle.B.X * triangle.C.Z - triangle.B.Z * triangle.C.X) +
                (double)triangle.A.Z * (triangle.B.X * triangle.C.Y - triangle.B.Y * triangle.C.X)) / 6.0;
        }

        return new GeometryMeasurements(bounds.ToBoundingBox(), surfaceArea, signedVolume);
    }

    private static bool IsFinite(Vector3 vertex)
    {
        return float.IsFinite(vertex.X) &&
               float.IsFinite(vertex.Y) &&
               float.IsFinite(vertex.Z);
    }

    private sealed class BoundsAccumulator
    {
        private bool _hasX;
        private bool _hasY;
        private bool _hasZ;
        private double _minX;
        private double _minY;
        private double _minZ;
        private double _maxX;
        private double _maxY;
        private double _maxZ;

        public void Include(Vector3 vertex)
        {
            Include(vertex.X, ref _minX, ref _maxX, ref _hasX);
            Include(vertex.Y, ref _minY, ref _maxY, ref _hasY);
            Include(vertex.Z, ref _minZ, ref _maxZ, ref _hasZ);
        }

        public MeshBoundingBox ToBoundingBox()
        {
            return _hasX || _hasY || _hasZ
                ? new MeshBoundingBox
                {
                    MinX = _minX,
                    MinY = _minY,
                    MinZ = _minZ,
                    MaxX = _maxX,
                    MaxY = _maxY,
                    MaxZ = _maxZ
                }
                : new MeshBoundingBox();
        }

        private static void Include(
            float value,
            ref double minimum,
            ref double maximum,
            ref bool hasValue)
        {
            if (!float.IsFinite(value))
            {
                return;
            }

            if (!hasValue)
            {
                minimum = maximum = value;
                hasValue = true;
                return;
            }

            minimum = Math.Min(minimum, value);
            maximum = Math.Max(maximum, value);
        }
    }

    private sealed record GeometryMeasurements(
        MeshBoundingBox BoundingBox,
        double SurfaceArea,
        double SignedVolume);
}
