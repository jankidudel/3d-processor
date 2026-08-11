using Processor.Models;

namespace Processor.Validation;

public class StlValidator
{
    private const double DegenerateRelativeTolerance = 1e-12;

    public ValidationResult Validate(Mesh mesh)
    {
        var errors = new List<string>();
        var triangleIndices = new Dictionary<TriangleKey, int>();
        var edgeMap = new Dictionary<EdgeKey, List<EdgeUse>>();

        if (mesh.Triangles.Count == 0)
        {
            errors.Add("Mesh contains no triangles.");
        }

        for (var i = 0; i < mesh.Triangles.Count; i++)
        {
            var triangle = mesh.Triangles[i];

            if (!HasValidCoordinates(triangle))
            {
                errors.Add($"Triangle {i} contains invalid coordinates.");
                continue;
            }

            if (IsDegenerate(triangle))
            {
                errors.Add($"Triangle {i} is degenerate.");
                continue;
            }

            var triangleKey = TriangleKey.Create(triangle);
            if (triangleIndices.TryGetValue(triangleKey, out var originalIndex))
            {
                errors.Add($"Triangle {i} duplicates triangle {originalIndex}.");
            }
            else
            {
                triangleIndices.Add(triangleKey, i);
            }

            AddEdge(edgeMap, triangle.A, triangle.B, i);
            AddEdge(edgeMap, triangle.B, triangle.C, i);
            AddEdge(edgeMap, triangle.C, triangle.A, i);
        }

        ValidateEdges(edgeMap, errors);

        return new ValidationResult
        {
            Errors = errors
        };
    }

    private static bool HasValidCoordinates(Triangle triangle)
    {
        return IsValidVector(triangle.A) &&
               IsValidVector(triangle.B) &&
               IsValidVector(triangle.C);
    }

    private static bool IsValidVector(Vector3 vector)
    {
        return float.IsFinite(vector.X) &&
               float.IsFinite(vector.Y) &&
               float.IsFinite(vector.Z);
    }

    private static bool IsDegenerate(Triangle triangle)
    {
        var abX = (double)triangle.B.X - triangle.A.X;
        var abY = (double)triangle.B.Y - triangle.A.Y;
        var abZ = (double)triangle.B.Z - triangle.A.Z;
        var acX = (double)triangle.C.X - triangle.A.X;
        var acY = (double)triangle.C.Y - triangle.A.Y;
        var acZ = (double)triangle.C.Z - triangle.A.Z;

        var crossX = abY * acZ - abZ * acY;
        var crossY = abZ * acX - abX * acZ;
        var crossZ = abX * acY - abY * acX;
        var twiceAreaSquared = crossX * crossX + crossY * crossY + crossZ * crossZ;

        var abLengthSquared = abX * abX + abY * abY + abZ * abZ;
        var acLengthSquared = acX * acX + acY * acY + acZ * acZ;
        var bcX = acX - abX;
        var bcY = acY - abY;
        var bcZ = acZ - abZ;
        var bcLengthSquared = bcX * bcX + bcY * bcY + bcZ * bcZ;
        var longestEdgeSquared = Math.Max(abLengthSquared, Math.Max(acLengthSquared, bcLengthSquared));

        return twiceAreaSquared <= DegenerateRelativeTolerance * longestEdgeSquared * longestEdgeSquared;
    }

    private static void AddEdge(
        Dictionary<EdgeKey, List<EdgeUse>> edgeMap,
        Vector3 start,
        Vector3 end,
        int triangleIndex)
    {
        var comparison = CompareVertices(start, end);
        var key = comparison < 0 ? new EdgeKey(start, end) : new EdgeKey(end, start);
        var use = new EdgeUse(triangleIndex, comparison < 0);

        if (!edgeMap.TryGetValue(key, out var uses))
        {
            uses = [];
            edgeMap.Add(key, uses);
        }

        uses.Add(use);
    }

    private static void ValidateEdges(
        Dictionary<EdgeKey, List<EdgeUse>> edgeMap,
        List<string> errors)
    {
        foreach (var (edge, uses) in edgeMap)
        {
            if (uses.Count == 1)
            {
                errors.Add(
                    $"Open edge {FormatEdge(edge)} is used only by triangle {uses[0].TriangleIndex}.");
            }
            else if (uses.Count > 2)
            {
                var triangleList = string.Join(", ", uses.Select(use => use.TriangleIndex));
                errors.Add(
                    $"Non-manifold edge {FormatEdge(edge)} is used by {uses.Count} triangles: {triangleList}.");
            }
            else if (uses[0].IsCanonicalDirection == uses[1].IsCanonicalDirection)
            {
                errors.Add(
                    $"Triangles {uses[0].TriangleIndex} and {uses[1].TriangleIndex} have inconsistent " +
                    $"orientation along edge {FormatEdge(edge)}.");
            }
        }
    }

    private static string FormatEdge(EdgeKey edge)
    {
        return $"[{FormatVertex(edge.Start)} - {FormatVertex(edge.End)}]";
    }

    private static string FormatVertex(Vector3 vertex)
    {
        return $"({vertex.X}, {vertex.Y}, {vertex.Z})";
    }

    private static int CompareVertices(Vector3 left, Vector3 right)
    {
        var comparison = left.X.CompareTo(right.X);
        if (comparison != 0)
        {
            return comparison;
        }

        comparison = left.Y.CompareTo(right.Y);
        return comparison != 0 ? comparison : left.Z.CompareTo(right.Z);
    }

    private readonly record struct EdgeKey(Vector3 Start, Vector3 End);

    private readonly record struct EdgeUse(int TriangleIndex, bool IsCanonicalDirection);

    private readonly record struct TriangleKey(Vector3 First, Vector3 Second, Vector3 Third)
    {
        public static TriangleKey Create(Triangle triangle)
        {
            var vertices = new[] { triangle.A, triangle.B, triangle.C };
            Array.Sort(vertices, CompareVertices);
            return new TriangleKey(vertices[0], vertices[1], vertices[2]);
        }
    }
}
