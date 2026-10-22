using Processor.Models;

namespace Processor.Reporting;

/// <summary>Accumulates measurements during validation without retaining geometry.</summary>
internal sealed class MeshGeometryAccumulator
{
    private readonly BoundsAccumulator bounds = new();
    private double surfaceArea;
    private double signedVolume;

    public MeshGeometryMeasurements Measurements => new(bounds.ToBoundingBox(), surfaceArea, signedVolume);

    public static MeshGeometryMeasurements Calculate(Mesh mesh)
    {
        var accumulator = new MeshGeometryAccumulator();
        foreach (var triangle in mesh.Triangles)
        {
            accumulator.Include(triangle);
        }
        return accumulator.Measurements;
    }

    public void Include(Triangle triangle)
    {
        bounds.Include(triangle.A);
        bounds.Include(triangle.B);
        bounds.Include(triangle.C);

        if (!IsFinite(triangle.A) || !IsFinite(triangle.B) || !IsFinite(triangle.C))
        {
            return;
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

    public void Include(MeshGeometryMeasurements measurements)
    {
        bounds.Include(measurements.BoundingBox);
        surfaceArea += measurements.SurfaceArea;
        signedVolume += measurements.SignedVolume;
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

        public void Include(MeshBoundingBox box)
        {
            Include(box.MinX, ref _minX, ref _maxX, ref _hasX);
            Include(box.MaxX, ref _minX, ref _maxX, ref _hasX);
            Include(box.MinY, ref _minY, ref _maxY, ref _hasY);
            Include(box.MaxY, ref _minY, ref _maxY, ref _hasY);
            Include(box.MinZ, ref _minZ, ref _maxZ, ref _hasZ);
            Include(box.MaxZ, ref _minZ, ref _maxZ, ref _hasZ);
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
            double value,
            ref double minimum,
            ref double maximum,
            ref bool hasValue)
        {
            if (!double.IsFinite(value))
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
}

internal sealed record MeshGeometryMeasurements(
    MeshBoundingBox BoundingBox,
    double SurfaceArea,
    double SignedVolume);
