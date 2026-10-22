using System.Runtime.InteropServices;
using Processor.Models;
using Processor.Reporting;
using Processor.Topology;

namespace Processor.Validation;

public class MeshValidator
{
    private const double DegenerateRelativeTolerance = 1e-12;

    public ValidationResult Validate(Mesh mesh)
    {
        var issues = new ValidationIssueCollector();
        var components = new MeshTopologyAnalyzer.ComponentTracker(mesh.Triangles.Count);
        var geometry = new MeshGeometryAccumulator();
        var highlights = new MeshIssueHighlightBuilder();
        var triangleIndices = new Dictionary<TriangleKey, int>(mesh.Triangles.Count);
        var edgeMap = new Dictionary<EdgeKey, EdgeUses>(checked(mesh.Triangles.Count * 3 / 2));

        if (mesh.Triangles.Count == 0)
        {
            issues.Add(new ValidationIssue(
                ValidationIssueCodes.EmptyMesh,
                "Mesh contains no triangles."));
        }

        for (var i = 0; i < mesh.Triangles.Count; i++)
        {
            var triangle = mesh.Triangles[i];
            components.Add(triangle, i);
            geometry.Include(triangle);

            if (!HasValidCoordinates(triangle))
            {
                issues.Add(ValidationIssueCodes.InvalidCoordinates,
                    issues.WantsExample(ValidationIssueCodes.InvalidCoordinates)
                        ? $"Triangle {i} contains invalid coordinates." : null);
                continue;
            }

            if (IsDegenerate(triangle))
            {
                issues.Add(ValidationIssueCodes.DegenerateTriangle,
                    issues.WantsExample(ValidationIssueCodes.DegenerateTriangle)
                        ? $"Triangle {i} is degenerate." : null);
                highlights.Add(ValidationIssueCodes.DegenerateTriangle, triangle.A, triangle.B, triangle.C);
                continue;
            }

            var triangleKey = TriangleKey.Create(triangle);
            if (triangleIndices.TryGetValue(triangleKey, out var originalIndex))
            {
                issues.Add(ValidationIssueCodes.DuplicateTriangle,
                    issues.WantsExample(ValidationIssueCodes.DuplicateTriangle)
                        ? $"Triangle {i} duplicates triangle {originalIndex}." : null);
                highlights.Add(ValidationIssueCodes.DuplicateTriangle, triangle.A, triangle.B, triangle.C);
            }
            else
            {
                triangleIndices.Add(triangleKey, i);
            }

            AddEdge(edgeMap, triangle.A, triangle.B, i);
            AddEdge(edgeMap, triangle.B, triangle.C, i);
            AddEdge(edgeMap, triangle.C, triangle.A, i);
        }

        var edgeValidation = ValidateEdges(edgeMap, issues, highlights);
        var topology = new MeshTopology
        {
            ConnectedComponentCount = components.Count,
            OpenEdgeCount = edgeValidation.OpenEdgeCount,
            NonManifoldEdgeCount = edgeValidation.NonManifoldEdgeCount,
            HasInconsistentOrientation = edgeValidation.HasInconsistentOrientation
        };

        if (topology.ConnectedComponentCount > 1)
        {
            issues.Add(new ValidationIssue(
                ValidationIssueCodes.MultipleDisconnectedShells,
                $"This STL contains {topology.ConnectedComponentCount} separate mesh components. " +
                "This can be intentional, but check that all parts are expected before printing.",
                topology.ConnectedComponentCount,
                ValidationIssueSeverity.Warning));
        }

        return new ValidationResult
        {
            Issues = issues.Build(),
            Topology = topology,
            IssueAnalysis = highlights.Build(),
            Geometry = geometry.Measurements
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
        Dictionary<EdgeKey, EdgeUses> edgeMap,
        Vector3 start,
        Vector3 end,
        int triangleIndex)
    {
        var comparison = CompareVertices(start, end);
        var key = comparison < 0 ? new EdgeKey(start, end) : new EdgeKey(end, start);
        // The common manifold case needs no per-edge object or list.
        ref var uses = ref CollectionsMarshal.GetValueRefOrAddDefault(edgeMap, key, out _);
        uses.Add(triangleIndex, comparison < 0);
    }

    private static EdgeValidationSummary ValidateEdges(
        Dictionary<EdgeKey, EdgeUses> edgeMap,
        ValidationIssueCollector issues,
        MeshIssueHighlightBuilder highlights)
    {
        var openEdgeCount = 0;
        var nonManifoldEdgeCount = 0;
        var hasInconsistentOrientation = false;
        foreach (var (edge, uses) in edgeMap)
        {
            if (uses.Count == 1)
            {
                openEdgeCount++;
                issues.Add(ValidationIssueCodes.OpenEdge,
                    issues.WantsExample(ValidationIssueCodes.OpenEdge)
                        ? $"Open edge {FormatEdge(edge)} is used only by triangle {uses.FirstIndex}." : null);
                highlights.AddOpenEdge(edge.Start, edge.End);
            }
            else if (uses.Count > 2)
            {
                nonManifoldEdgeCount++;
                issues.Add(ValidationIssueCodes.NonManifoldEdge,
                    issues.WantsExample(ValidationIssueCodes.NonManifoldEdge)
                        ? $"Non-manifold edge {FormatEdge(edge)} is used by {uses.Count} triangles: {uses.FormatTriangleExamples()}." : null);
                highlights.AddNonManifoldEdge(edge.Start, edge.End);
            }
            else if (uses.FirstDirection == uses.SecondDirection)
            {
                hasInconsistentOrientation = true;
                issues.Add(ValidationIssueCodes.InconsistentOrientation,
                    issues.WantsExample(ValidationIssueCodes.InconsistentOrientation)
                        ? $"Triangles {uses.FirstIndex} and {uses.SecondIndex} have inconsistent " +
                          $"orientation along edge {FormatEdge(edge)}." : null);
                highlights.Add(ValidationIssueCodes.InconsistentOrientation, edge.Start, edge.End);
            }
        }

        return new EdgeValidationSummary(
            openEdgeCount,
            nonManifoldEdgeCount,
            hasInconsistentOrientation);
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

    private struct EdgeUses
    {
        public int Count;
        public int FirstIndex;
        public int SecondIndex;
        public int ThirdIndex;
        public bool FirstDirection;
        public bool SecondDirection;

        public void Add(int index, bool direction)
        {
            Count++;
            if (Count == 1)
            {
                FirstIndex = index;
                FirstDirection = direction;
            }
            else if (Count == 2)
            {
                SecondIndex = index;
                SecondDirection = direction;
            }
            else if (Count == 3)
            {
                ThirdIndex = index;
            }
        }

        public readonly string FormatTriangleExamples()
        {
            return Count == 3
                ? $"{FirstIndex}, {SecondIndex}, {ThirdIndex}"
                : $"{FirstIndex}, {SecondIndex}, {ThirdIndex}, ...";
        }
    }

    private readonly record struct EdgeValidationSummary(
        int OpenEdgeCount,
        int NonManifoldEdgeCount,
        bool HasInconsistentOrientation);

    private readonly record struct TriangleKey(Vector3 First, Vector3 Second, Vector3 Third)
    {
        public static TriangleKey Create(Triangle triangle)
        {
            var first = triangle.A;
            var second = triangle.B;
            var third = triangle.C;
            if (CompareVertices(first, second) > 0)
            {
                (first, second) = (second, first);
            }
            if (CompareVertices(second, third) > 0)
            {
                (second, third) = (third, second);
            }
            if (CompareVertices(first, second) > 0)
            {
                (first, second) = (second, first);
            }
            return new TriangleKey(first, second, third);
        }
    }
}
