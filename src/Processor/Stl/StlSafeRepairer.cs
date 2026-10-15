using Processor.Models;

namespace Processor.Stl;

public sealed class StlSafeRepairer
{
    private const double DegenerateRelativeTolerance = 1e-12;

    public SafeRepairResult Repair(Mesh mesh)
    {
        if (mesh.Triangles.Any(triangle => !HasValidCoordinates(triangle)))
        {
            return new SafeRepairResult(null, 0, 0, 0, ["Invalid coordinates prevent safe automatic repair."]);
        }

        var retained = new List<Triangle>();
        var seen = new HashSet<TriangleKey>();
        var duplicatesRemoved = 0;
        var degeneratesRemoved = 0;
        foreach (var triangle in mesh.Triangles)
        {
            if (IsDegenerate(triangle))
            {
                degeneratesRemoved++;
                continue;
            }
            if (!seen.Add(TriangleKey.Create(triangle)))
            {
                duplicatesRemoved++;
                continue;
            }
            retained.Add(triangle);
        }

        var orientation = OrientClosedManifoldRegions(retained);
        var changed = duplicatesRemoved > 0 || degeneratesRemoved > 0 || orientation.CorrectedCount > 0;
        var cleanedMesh = new Mesh();
        cleanedMesh.Triangles.AddRange(retained);
        return new SafeRepairResult(
            changed ? cleanedMesh : null,
            duplicatesRemoved,
            degeneratesRemoved,
            orientation.CorrectedCount,
            orientation.SkippedReasons);
    }

    private static OrientationResult OrientClosedManifoldRegions(List<Triangle> triangles)
    {
        var edges = new Dictionary<EdgeKey, List<EdgeUse>>();
        for (var index = 0; index < triangles.Count; index++)
        {
            AddEdge(edges, triangles[index].A, triangles[index].B, index);
            AddEdge(edges, triangles[index].B, triangles[index].C, index);
            AddEdge(edges, triangles[index].C, triangles[index].A, index);
        }

        var neighbours = Enumerable.Range(0, triangles.Count).Select(_ => new List<(int Index, EdgeUse ThisUse, EdgeUse OtherUse)>()).ToArray();
        var unsafeTriangles = new HashSet<int>();
        foreach (var uses in edges.Values)
        {
            if (uses.Count != 2)
            {
                foreach (var use in uses)
                {
                    unsafeTriangles.Add(use.TriangleIndex);
                }
                continue;
            }
            neighbours[uses[0].TriangleIndex].Add((uses[1].TriangleIndex, uses[0], uses[1]));
            neighbours[uses[1].TriangleIndex].Add((uses[0].TriangleIndex, uses[1], uses[0]));
        }

        var visited = new bool[triangles.Count];
        var corrected = 0;
        var skipped = new List<string>();
        for (var start = 0; start < triangles.Count; start++)
        {
            if (visited[start])
            {
                continue;
            }
            var component = new List<int>();
            var flips = new Dictionary<int, bool> { [start] = false };
            var queue = new Queue<int>();
            queue.Enqueue(start);
            visited[start] = true;
            var canOrient = true;
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                component.Add(current);
                if (unsafeTriangles.Contains(current))
                {
                    canOrient = false;
                }
                foreach (var neighbour in neighbours[current].OrderBy(item => item.Index))
                {
                    var expectedFlip = neighbour.ThisUse.IsCanonicalDirection ^ flips[current] ^ neighbour.OtherUse.IsCanonicalDirection ^ true;
                    if (flips.TryGetValue(neighbour.Index, out var existingFlip))
                    {
                        if (existingFlip != expectedFlip)
                        {
                            canOrient = false;
                        }
                        continue;
                    }
                    flips.Add(neighbour.Index, expectedFlip);
                    visited[neighbour.Index] = true;
                    queue.Enqueue(neighbour.Index);
                }
            }
            if (!canOrient)
            {
                skipped.Add("An open, non-manifold or contradictory region was not re-oriented.");
                continue;
            }
            // Both parity assignments satisfy shared-edge consistency. Prefer
            // the smaller edit; the graph alone cannot identify the outside.
            var invertAssignment = component.Count(index => flips[index]) > component.Count / 2;
            foreach (var index in component)
            {
                if (flips[index] == invertAssignment)
                {
                    continue;
                }
                var triangle = triangles[index];
                triangles[index] = new Triangle(triangle.Normal, triangle.A, triangle.C, triangle.B);
                corrected++;
            }
        }
        return new OrientationResult(corrected, skipped.Distinct(StringComparer.Ordinal).ToArray());
    }

    private static bool HasValidCoordinates(Triangle triangle) =>
        IsFinite(triangle.A) && IsFinite(triangle.B) && IsFinite(triangle.C);

    private static bool IsFinite(Vector3 value) => float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z);

    private static bool IsDegenerate(Triangle triangle)
    {
        if (triangle.A == triangle.B || triangle.B == triangle.C || triangle.C == triangle.A)
        {
            return true;
        }
        var ab = Subtract(triangle.B, triangle.A);
        var ac = Subtract(triangle.C, triangle.A);
        var bc = Subtract(triangle.C, triangle.B);
        var cross = Cross(ab, ac);
        var twiceAreaSquared = Dot(cross, cross);
        var longestEdgeSquared = Math.Max(Dot(ab, ab), Math.Max(Dot(ac, ac), Dot(bc, bc)));
        return twiceAreaSquared <= DegenerateRelativeTolerance * longestEdgeSquared * longestEdgeSquared;
    }

    private static Vector3 Subtract(Vector3 left, Vector3 right) => new(left.X - right.X, left.Y - right.Y, left.Z - right.Z);
    private static Vector3 Cross(Vector3 left, Vector3 right) => new(left.Y * right.Z - left.Z * right.Y, left.Z * right.X - left.X * right.Z, left.X * right.Y - left.Y * right.X);
    private static double Dot(Vector3 left, Vector3 right) => (double)left.X * right.X + (double)left.Y * right.Y + (double)left.Z * right.Z;

    private static void AddEdge(Dictionary<EdgeKey, List<EdgeUse>> edges, Vector3 start, Vector3 end, int triangleIndex)
    {
        var comparison = Compare(start, end);
        var key = comparison < 0 ? new EdgeKey(start, end) : new EdgeKey(end, start);
        if (!edges.TryGetValue(key, out var uses))
        {
            uses = [];
            edges.Add(key, uses);
        }
        uses.Add(new EdgeUse(triangleIndex, comparison < 0));
    }

    private static int Compare(Vector3 left, Vector3 right)
    {
        var comparison = left.X.CompareTo(right.X);
        if (comparison != 0)
        {
            return comparison;
        }

        comparison = left.Y.CompareTo(right.Y);
        return comparison != 0 ? comparison : left.Z.CompareTo(right.Z);
    }

    public sealed record SafeRepairResult(Mesh? CleanedMesh, int DuplicatesRemoved, int DegeneratesRemoved, int OrientationsCorrected, IReadOnlyList<string> SkippedReasons);
    private readonly record struct EdgeKey(Vector3 Start, Vector3 End);
    private readonly record struct EdgeUse(int TriangleIndex, bool IsCanonicalDirection);
    private readonly record struct TriangleKey(Vector3 First, Vector3 Second, Vector3 Third)
    {
        public static TriangleKey Create(Triangle triangle)
        {
            var vertices = new[] { triangle.A, triangle.B, triangle.C };
            Array.Sort(vertices, Compare);
            return new TriangleKey(vertices[0], vertices[1], vertices[2]);
        }
    }
    private sealed record OrientationResult(int CorrectedCount, IReadOnlyList<string> SkippedReasons);
}
