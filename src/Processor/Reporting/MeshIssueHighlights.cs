using Processor.Models;
using Processor.Validation;

namespace Processor.Reporting;

public sealed class MeshIssueHighlights
{
    public const int SampleLimit = 100;
    public Dictionary<string, List<MeshIssueHighlight>> Samples { get; init; } = [];
}

public sealed record MeshIssueHighlight(Vector3 A, Vector3 B, Vector3? C = null);

internal static class MeshIssueHighlightSampler
{
    public static MeshIssueHighlights Sample(Mesh mesh)
    {
        return Analyze(mesh).Highlights;
    }

    public static MeshIssueHighlightAnalysis Analyze(Mesh mesh)
    {
        var samples = new Dictionary<string, List<MeshIssueHighlight>>();
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
        var openEdges = new List<MeshIssueEdgeSegment>();
        var nonManifoldEdges = new List<MeshIssueEdgeSegment>();
        var openEdgeCount = 0;
        var nonManifoldEdgeCount = 0;
        foreach (var (edge, uses) in edges)
        {
            if (uses.Count == 1)
            {
                openEdgeCount++;
                Add(samples, ValidationIssueCodes.OpenEdge, new(edge.A, edge.B));
                AddSegment(openEdges, edge);
            }
            else if (uses.Count > 2)
            {
                nonManifoldEdgeCount++;
                Add(samples, ValidationIssueCodes.NonManifoldEdge, new(edge.A, edge.B));
                AddSegment(nonManifoldEdges, edge);
            }
            else if (uses[0].Direction == uses[1].Direction)
            {
                Add(samples, ValidationIssueCodes.InconsistentOrientation, new(edge.A, edge.B));
            }
        }
        return new MeshIssueHighlightAnalysis(
            new MeshIssueHighlights { Samples = samples },
            new MeshIssueGeometry
            {
                OpenEdges = ToSegments(openEdgeCount, openEdges),
                NonManifoldEdges = ToSegments(nonManifoldEdgeCount, nonManifoldEdges)
            });
    }

    /// <summary>
    /// Samples every supplied mesh independently. Coincident edges in separate
    /// meshes remain separate diagnostic markers, matching aggregate topology
    /// reports that validate each mesh independently.
    /// </summary>
    public static MeshIssueHighlightAnalysis Analyze(IEnumerable<Mesh> meshes)
    {
        var highlights = new Dictionary<string, List<MeshIssueHighlight>>();
        var openEdges = new List<MeshIssueEdgeSegment>();
        var nonManifoldEdges = new List<MeshIssueEdgeSegment>();
        var openEdgeCount = 0;
        var nonManifoldEdgeCount = 0;

        foreach (var mesh in meshes)
        {
            var analysis = Analyze(mesh);
            AppendHighlights(highlights, analysis.Highlights.Samples);
            AppendSegments(openEdges, analysis.Geometry.OpenEdges, ref openEdgeCount);
            AppendSegments(nonManifoldEdges, analysis.Geometry.NonManifoldEdges, ref nonManifoldEdgeCount);
        }

        return new MeshIssueHighlightAnalysis(
            new MeshIssueHighlights { Samples = highlights },
            new MeshIssueGeometry
            {
                OpenEdges = ToSegments(openEdgeCount, openEdges),
                NonManifoldEdges = ToSegments(nonManifoldEdgeCount, nonManifoldEdges)
            });
    }

    private static void AppendHighlights(
        Dictionary<string, List<MeshIssueHighlight>> destination,
        Dictionary<string, List<MeshIssueHighlight>> source)
    {
        foreach (var (code, samples) in source)
        {
            foreach (var sample in samples)
            {
                Add(destination, code, sample);
            }
        }
    }

    private static void AppendSegments(
        List<MeshIssueEdgeSegment> destination,
        MeshIssueEdgeSegments? source,
        ref int totalCount)
    {
        if (source is null)
        {
            return;
        }
        totalCount += source.TotalCount;
        foreach (var segment in source.Segments)
        {
            if (destination.Count < MeshIssueGeometry.SegmentLimit)
            {
                destination.Add(segment);
            }
        }
    }

    private static void AddSegment(List<MeshIssueEdgeSegment> segments, EdgeKey edge)
    {
        if (segments.Count < MeshIssueGeometry.SegmentLimit && Finite(edge.A) && Finite(edge.B))
        {
            segments.Add(new MeshIssueEdgeSegment(edge.A, edge.B));
        }
    }

    private static MeshIssueEdgeSegments ToSegments(int totalCount, List<MeshIssueEdgeSegment> segments)
    {
        return new MeshIssueEdgeSegments
        {
            TotalCount = totalCount,
            ReturnedCount = segments.Count,
            Truncated = totalCount > segments.Count,
            Segments = segments
        };
    }

    private static void Add(Dictionary<string, List<MeshIssueHighlight>> samples, string code, MeshIssueHighlight sample)
    {
        if (!samples.TryGetValue(code, out var values))
        {
            values = [];
            samples.Add(code, values);
        }
        if (values.Count < MeshIssueHighlights.SampleLimit)
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
    private static bool Finite(Triangle triangle) => Finite(triangle.A) && Finite(triangle.B) && Finite(triangle.C);
    private static bool Finite(Vector3 vector) => float.IsFinite(vector.X) && float.IsFinite(vector.Y) && float.IsFinite(vector.Z);
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

internal sealed record MeshIssueHighlightAnalysis(
    MeshIssueHighlights Highlights,
    MeshIssueGeometry Geometry);
