using Processor.Models;

namespace Processor.Stl;

/// <summary>Conservative axis-aligned STL splitter. Coordinates within 1e-6 of
/// the model extent are merged while constructing cut loops; a loop which is not
/// closed is rejected instead of producing an open part.</summary>
public sealed class StlSplitter
{
    private const double RelativeTolerance = 1e-6;

    public SplitResult Split(Mesh mesh, SplitAxis axis, double position)
    {
        var extent = Extent(mesh, axis);
        var tolerance = Math.Max(1e-6, extent * RelativeTolerance);
        var a = new List<Triangle>();
        var b = new List<Triangle>();
        var segments = new List<Segment>();
        foreach (var triangle in mesh.Triangles)
        {
            var values = new[] { Coordinate(triangle.A, axis) - position, Coordinate(triangle.B, axis) - position, Coordinate(triangle.C, axis) - position };
            if (values.All(value => value <= tolerance))
            {
                AddTriangle(a, triangle.A, triangle.B, triangle.C);
                continue;
            }
            if (values.All(value => value >= -tolerance))
            {
                AddTriangle(b, triangle.A, triangle.B, triangle.C);
                continue;
            }
            var vertices = new[] { triangle.A, triangle.B, triangle.C };
            AddPolygon(a, Clip(vertices, axis, position, true, tolerance));
            AddPolygon(b, Clip(vertices, axis, position, false, tolerance));
            var points = Intersections(vertices, values, axis, position, tolerance);
            if (points.Count == 2 && !Near(points[0], points[1], tolerance))
            {
                segments.Add(new Segment(points[0], points[1]));
            }
            else if (points.Count != 0)
            {
                throw new SplitException("The cut touches ambiguous triangle geometry and cannot be capped safely.");
            }
        }
        if (a.Count == 0 || b.Count == 0 || segments.Count == 0)
        {
            throw new SplitException("The cut must pass through the model and leave two non-empty parts.");
        }
        var loops = BuildLoops(segments, tolerance);
        foreach (var loop in loops)
        {
            Cap(a, loop, axis, position, false, tolerance);
            Cap(b, loop, axis, position, true, tolerance);
        }
        return new SplitResult(ToMesh(a), ToMesh(b));
    }

    private static Mesh ToMesh(List<Triangle> triangles)
    {
        var mesh = new Mesh();
        mesh.Triangles.AddRange(triangles);
        return mesh;
    }
    private static double Extent(Mesh mesh, SplitAxis axis) => mesh.Triangles.SelectMany(t => new[] { Coordinate(t.A, axis), Coordinate(t.B, axis), Coordinate(t.C, axis) }).Max() - mesh.Triangles.SelectMany(t => new[] { Coordinate(t.A, axis), Coordinate(t.B, axis), Coordinate(t.C, axis) }).Min();
    private static double Coordinate(Vector3 point, SplitAxis axis) => axis switch { SplitAxis.X => point.X, SplitAxis.Y => point.Y, _ => point.Z };
    private static Vector3 SetCoordinate(Vector3 point, SplitAxis axis, double value) => axis switch { SplitAxis.X => point with { X = (float)value }, SplitAxis.Y => point with { Y = (float)value }, _ => point with { Z = (float)value } };
    private static List<Vector3> Clip(IReadOnlyList<Vector3> input, SplitAxis axis, double position, bool lower, double tolerance)
    {
        var output = new List<Vector3>();
        for (var i = 0; i < input.Count; i++)
        {
            var current = input[i];
            var next = input[(i + 1) % input.Count];
            var currentValue = Coordinate(current, axis) - position;
            var nextValue = Coordinate(next, axis) - position;
            var currentInside = lower ? currentValue <= tolerance : currentValue >= -tolerance;
            var nextInside = lower ? nextValue <= tolerance : nextValue >= -tolerance;
            if (currentInside)
            {
                output.Add(Math.Abs(currentValue) <= tolerance ? SetCoordinate(current, axis, position) : current);
            }
            if (currentInside != nextInside)
            {
                var ratio = currentValue / (currentValue - nextValue);
                output.Add(SetCoordinate(Lerp(current, next, ratio), axis, position));
            }
        }
        return DistinctAdjacent(output, tolerance);
    }
    private static List<Vector3> Intersections(IReadOnlyList<Vector3> vertices, IReadOnlyList<double> values, SplitAxis axis, double position, double tolerance)
    {
        var points = new List<Vector3>();
        for (var i = 0; i < 3; i++)
        {
            var j = (i + 1) % 3;
            var a = values[i];
            var b = values[j];
            if (Math.Abs(a) <= tolerance)
            {
                AddUnique(points, SetCoordinate(vertices[i], axis, position), tolerance);
            }
            if ((a < -tolerance && b > tolerance) || (a > tolerance && b < -tolerance))
            {
                AddUnique(points, SetCoordinate(Lerp(vertices[i], vertices[j], a / (a - b)), axis, position), tolerance);
            }
        }
        return points;
    }
    private static void AddPolygon(List<Triangle> target, List<Vector3> polygon)
    {
        for (var i = 1; i + 1 < polygon.Count; i++)
        {
            AddTriangle(target, polygon[0], polygon[i], polygon[i + 1]);
        }
    }
    private static void AddTriangle(List<Triangle> target, Vector3 a, Vector3 b, Vector3 c)
    {
        var normal = Normal(a, b, c);
        if (Length(normal) > 1e-10)
        {
            target.Add(new Triangle(normal, a, b, c));
        }
    }
    private static List<List<Vector3>> BuildLoops(List<Segment> segments, double tolerance)
    {
        var unused = new List<Segment>(segments);
        var loops = new List<List<Vector3>>();
        while (unused.Count > 0)
        {
            var first = unused[0];
            unused.RemoveAt(0);
            var loop = new List<Vector3> { first.A, first.B };
            while (!Near(loop[^1], loop[0], tolerance))
            {
                var index = unused.FindIndex(segment => Near(segment.A, loop[^1], tolerance) || Near(segment.B, loop[^1], tolerance));
                if (index < 0)
                {
                    throw new SplitException("The cut contour could not be assembled into a closed loop.");
                }
                var next = unused[index];
                unused.RemoveAt(index);
                loop.Add(Near(next.A, loop[^1], tolerance) ? next.B : next.A);
                if (loop.Count > segments.Count + 1)
                {
                    throw new SplitException("The cut contour is ambiguous and cannot be capped safely.");
                }
            }
            loop.RemoveAt(loop.Count - 1);
            if (loop.Count < 3)
            {
                throw new SplitException("The cut contour is too small to cap safely.");
            }
            loops.Add(loop);
        }
        return loops;
    }
    private static void Cap(List<Triangle> target, List<Vector3> loop, SplitAxis axis, double position, bool positiveNormal, double tolerance)
    {
        var normal = PolygonNormal(loop, axis);
        var desired = axis switch
        {
            SplitAxis.X => new Vector3(positiveNormal ? 1 : -1, 0, 0),
            SplitAxis.Y => new Vector3(0, positiveNormal ? 1 : -1, 0),
            _ => new Vector3(0, 0, positiveNormal ? 1 : -1)
        };
        if (Dot(normal, desired) < 0)
        {
            loop = loop.AsEnumerable().Reverse().ToList();
        }
        // Cut contours can be concave. Ear clipping is deterministic and avoids a fan escaping the contour.
        var indices = Enumerable.Range(0, loop.Count).ToList();
        while (indices.Count > 2)
        {
            var ear = -1;
            for (var i = 0; i < indices.Count; i++)
            {
                var prev = loop[indices[(i - 1 + indices.Count) % indices.Count]];
                var current = loop[indices[i]];
                var next = loop[indices[(i + 1) % indices.Count]];
                if (Dot(Normal(prev, current, next), desired) <= tolerance)
                {
                    continue;
                }
                if (!indices.Where((_, n) => n != i && n != (i - 1 + indices.Count) % indices.Count && n != (i + 1) % indices.Count).Any(n => InTriangle(loop[n], prev, current, next, axis, tolerance)))
                {
                    ear = i;
                    break;
                }
            }
            if (ear < 0)
            {
                throw new SplitException("The cut contour could not be triangulated safely.");
            }
            var previous = indices[(ear - 1 + indices.Count) % indices.Count];
            var currentIndex = indices[ear];
            var nextIndex = indices[(ear + 1) % indices.Count];
            AddTriangle(target, loop[previous], loop[currentIndex], loop[nextIndex]);
            indices.RemoveAt(ear);
        }
    }
    private static bool InTriangle(Vector3 point, Vector3 a, Vector3 b, Vector3 c, SplitAxis axis, double tolerance)
    {
        var p = Project(point, axis);
        var pa = Project(a, axis);
        var pb = Project(b, axis);
        var pc = Project(c, axis);
        var area = Cross2(pa, pb, pc);
        var first = Cross2(p, pa, pb);
        var second = Cross2(p, pb, pc);
        var third = Cross2(p, pc, pa);
        return area >= 0 ? first >= -tolerance && second >= -tolerance && third >= -tolerance : first <= tolerance && second <= tolerance && third <= tolerance;
    }
    private static (double X, double Y) Project(Vector3 p, SplitAxis axis) => axis switch { SplitAxis.X => (p.Y, p.Z), SplitAxis.Y => (p.X, p.Z), _ => (p.X, p.Y) };
    private static double Cross2((double X, double Y) a, (double X, double Y) b, (double X, double Y) c) => (b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X);
    private static Vector3 PolygonNormal(List<Vector3> points, SplitAxis axis)
    {
        var sum = 0d;
        for (var i = 0; i < points.Count; i++)
        {
            var a = Project(points[i], axis);
            var b = Project(points[(i + 1) % points.Count], axis);
            sum += a.X * b.Y - a.Y * b.X;
        }
        return axis switch
        {
            SplitAxis.X => new Vector3((float)sum, 0, 0),
            SplitAxis.Y => new Vector3(0, (float)-sum, 0),
            _ => new Vector3(0, 0, (float)sum)
        };
    }
    private static List<Vector3> DistinctAdjacent(List<Vector3> values, double tolerance)
    {
        var result = new List<Vector3>();
        foreach (var value in values)
        {
            AddUnique(result, value, tolerance);
        }
        if (result.Count > 1 && Near(result[0], result[^1], tolerance))
        {
            result.RemoveAt(result.Count - 1);
        }
        return result;
    }
    private static void AddUnique(List<Vector3> values, Vector3 value, double tolerance)
    {
        if (!values.Any(existing => Near(existing, value, tolerance)))
        {
            values.Add(value);
        }
    }
    private static bool Near(Vector3 a, Vector3 b, double tolerance) => Math.Abs(a.X - b.X) <= tolerance && Math.Abs(a.Y - b.Y) <= tolerance && Math.Abs(a.Z - b.Z) <= tolerance;
    private static Vector3 Lerp(Vector3 a, Vector3 b, double ratio) => new((float)(a.X + (b.X - a.X) * ratio), (float)(a.Y + (b.Y - a.Y) * ratio), (float)(a.Z + (b.Z - a.Z) * ratio));
    private static Vector3 Normal(Vector3 a, Vector3 b, Vector3 c) => new((b.Y - a.Y) * (c.Z - a.Z) - (b.Z - a.Z) * (c.Y - a.Y), (b.Z - a.Z) * (c.X - a.X) - (b.X - a.X) * (c.Z - a.Z), (b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X));
    private static double Dot(Vector3 a, Vector3 b) => (double)a.X * b.X + (double)a.Y * b.Y + (double)a.Z * b.Z;
    private static double Length(Vector3 p) => Math.Sqrt(Dot(p, p));
    private readonly record struct Segment(Vector3 A, Vector3 B);
}

public enum SplitAxis
{
    X, Y, Z
}
public sealed class SplitException(string message) : Exception(message);
public sealed record SplitResult(Mesh PartA, Mesh PartB);
