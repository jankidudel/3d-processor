using Processor.Models;
using Processor.Validation;

namespace Processor.Reporting;

public sealed class StlIssueHighlights
{
    public const int SampleLimit = 100;
    public Dictionary<string, List<StlIssueHighlight>> Samples { get; init; } = [];
    public int SampleLimitPerIssue => SampleLimit;
}

public sealed record StlIssueHighlight(Vector3 A, Vector3 B, Vector3? C = null);

internal static class StlIssueHighlightSampler
{
    public static StlIssueHighlights Sample(Mesh mesh)
    {
        var samples = new Dictionary<string, List<StlIssueHighlight>>();
        var edges = new Dictionary<EdgeKey, List<(int Index, bool Direction)>>();
        var triangles = new Dictionary<TriangleKey, int>();
        for (var index = 0; index < mesh.Triangles.Count; index++)
        {
            var triangle = mesh.Triangles[index];
            if (!Finite(triangle))
            {
                continue;
            }
            if (Degenerate(triangle))
            {
                Add(samples, ValidationIssueCodes.DegenerateTriangle, new(triangle.A, triangle.B, triangle.C));
                continue;
            }
            var key = TriangleKey.Create(triangle);
            if (!triangles.TryAdd(key, index))
            {
                Add(samples, ValidationIssueCodes.DuplicateTriangle, new(triangle.A, triangle.B, triangle.C));
            }
            AddEdge(edges, triangle.A, triangle.B, index);
            AddEdge(edges, triangle.B, triangle.C, index);
            AddEdge(edges, triangle.C, triangle.A, index);
        }
        foreach (var (edge, uses) in edges)
        {
            if (uses.Count == 1)
            {
                Add(samples, ValidationIssueCodes.OpenEdge, new(edge.A, edge.B));
            }
            else if (uses.Count > 2)
            {
                Add(samples, ValidationIssueCodes.NonManifoldEdge, new(edge.A, edge.B));
            }
            else if (uses[0].Direction == uses[1].Direction)
            {
                Add(samples, ValidationIssueCodes.InconsistentOrientation, new(edge.A, edge.B));
            }
        }
        return new StlIssueHighlights { Samples = samples };
    }

    private static void Add(Dictionary<string, List<StlIssueHighlight>> samples, string code, StlIssueHighlight sample)
    {
        if (!samples.TryGetValue(code, out var values))
        {
            values = [];
            samples.Add(code, values);
        }
        if (values.Count < StlIssueHighlights.SampleLimit)
        {
            values.Add(sample);
        }
    }
    private static void AddEdge(Dictionary<EdgeKey, List<(int, bool)>> edges, Vector3 first, Vector3 second, int index)
    {
        var direction = Compare(first, second) < 0;
        var key = direction ? new EdgeKey(first, second) : new EdgeKey(second, first);
        if (!edges.TryGetValue(key, out var uses))
        {
            uses = [];
            edges.Add(key, uses);
        }
        uses.Add((index, direction));
    }
    private static bool Finite(Triangle triangle) => float.IsFinite(triangle.A.X) && float.IsFinite(triangle.A.Y) && float.IsFinite(triangle.A.Z) && float.IsFinite(triangle.B.X) && float.IsFinite(triangle.B.Y) && float.IsFinite(triangle.B.Z) && float.IsFinite(triangle.C.X) && float.IsFinite(triangle.C.Y) && float.IsFinite(triangle.C.Z);
    private static bool Degenerate(Triangle triangle)
    {
        var ab = Difference(triangle.B, triangle.A); var ac = Difference(triangle.C, triangle.A); var bc = Difference(triangle.C, triangle.B);
        var crossX = ab.X * ac.Z - ab.Z * ac.Y; var crossY = ab.Z * ac.X - ab.X * ac.Z; var crossZ = ab.X * ac.Y - ab.Y * ac.X;
        var area = crossX * crossX + crossY * crossY + crossZ * crossZ;
        var longest = Math.Max(LengthSquared(ab), Math.Max(LengthSquared(ac), LengthSquared(bc)));
        return area <= 1e-12 * longest * longest;
    }
    private static (double X, double Y, double Z) Difference(Vector3 first, Vector3 second) => ((double)first.X - second.X, (double)first.Y - second.Y, (double)first.Z - second.Z);
    private static double LengthSquared((double X, double Y, double Z) value) => value.X * value.X + value.Y * value.Y + value.Z * value.Z;
    private static int Compare(Vector3 left, Vector3 right) { var result = left.X.CompareTo(right.X); if (result != 0) { return result; } result = left.Y.CompareTo(right.Y); return result != 0 ? result : left.Z.CompareTo(right.Z); }
    private readonly record struct EdgeKey(Vector3 A, Vector3 B);
    private readonly record struct TriangleKey(Vector3 A, Vector3 B, Vector3 C)
    {
        public static TriangleKey Create(Triangle triangle) { var points = new[] { triangle.A, triangle.B, triangle.C }; Array.Sort(points, Compare); return new(points[0], points[1], points[2]); }
    }
}
