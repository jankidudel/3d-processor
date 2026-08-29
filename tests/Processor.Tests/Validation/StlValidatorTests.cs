using Processor.Models;
using Processor.Validation;

namespace Processor.Tests.Validation;

public class MeshValidatorTests
{
    private static readonly Vector3 ZeroNormal = new(0, 0, 0);

    [Fact]
    public void Validate_EmptyMesh_IsInvalid()
    {
        var result = Validate();

        Assert.False(result.IsValid);
        var issue = Assert.Single(result.Issues);
        Assert.Equal(ValidationIssueCodes.EmptyMesh, issue.Code);
        Assert.Equal("Mesh contains no triangles.", issue.Message);
    }

    [Fact]
    public void Validate_ClosedConsistentlyOrientedMesh_IsValid()
    {
        var result = Validate(CreateTetrahedron());

        Assert.True(result.IsValid);
        Assert.Empty(result.Issues);
        Assert.Equal(0, result.Topology.OpenEdgeCount);
        Assert.Equal(0, result.Topology.NonManifoldEdgeCount);
        Assert.False(result.Topology.HasInconsistentOrientation);
    }

    [Fact]
    public void Validate_SingleConnectedShell_ReportsOneComponent()
    {
        var result = Validate(CreateTetrahedron());

        Assert.Equal(1, result.Topology.ConnectedComponentCount);
        Assert.DoesNotContain(result.Issues, issue =>
            issue.Code == ValidationIssueCodes.MultipleDisconnectedShells);
    }

    [Fact]
    public void Validate_TwoDisconnectedClosedShells_ReportsWarningButRemainsValid()
    {
        var result = Validate(
        [
            .. CreateTetrahedron(),
            .. CreateTetrahedron(new Vector3(3, 0, 0))
        ]);

        Assert.Equal(2, result.Topology.ConnectedComponentCount);
        Assert.True(result.IsValid);
        var issue = Assert.Single(result.Issues);
        Assert.Equal(ValidationIssueCodes.MultipleDisconnectedShells, issue.Code);
        Assert.Equal(ValidationIssueSeverity.Warning, issue.Severity);
        Assert.Equal(2, issue.Count);
    }

    [Fact]
    public void Validate_MultipleDisconnectedShells_ReportsAllComponents()
    {
        var result = Validate(
        [
            .. CreateTetrahedron(),
            .. CreateTetrahedron(new Vector3(3, 0, 0)),
            .. CreateTetrahedron(new Vector3(6, 0, 0))
        ]);

        Assert.Equal(3, result.Topology.ConnectedComponentCount);
        Assert.Equal(3, result.CountIssues(ValidationIssueCodes.MultipleDisconnectedShells));
    }

    [Fact]
    public void Validate_ShellsTouchingAtOneVertex_AreOneComponent()
    {
        var result = Validate(
        [
            .. CreateTetrahedron(),
            .. CreateTetrahedronTouchingOrigin()
        ]);

        Assert.Equal(1, result.Topology.ConnectedComponentCount);
        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    [InlineData(float.NegativeInfinity)]
    public void Validate_NonFiniteVertexCoordinate_IsRejected(float invalidCoordinate)
    {
        var triangle = CreateTriangle(
            new Vector3(invalidCoordinate, 0, 0),
            new Vector3(1, 0, 0),
            new Vector3(0, 1, 0));

        var result = Validate(triangle);

        Assert.Contains(result.Issues, issue =>
            issue.Code == ValidationIssueCodes.InvalidCoordinates &&
            issue.Message == "Triangle 0 contains invalid coordinates.");
    }

    [Fact]
    public void Validate_RepeatedVertex_IsDegenerate()
    {
        var a = new Vector3(0, 0, 0);
        var triangle = CreateTriangle(a, a, new Vector3(1, 0, 0));

        var result = Validate(triangle);

        AssertIssue(result, ValidationIssueCodes.DegenerateTriangle, "Triangle 0 is degenerate.");
    }

    [Fact]
    public void Validate_CollinearVertices_AreDegenerate()
    {
        var triangle = CreateTriangle(
            new Vector3(0, 0, 0),
            new Vector3(1, 1, 1),
            new Vector3(2, 2, 2));

        var result = Validate(triangle);

        AssertIssue(result, ValidationIssueCodes.DegenerateTriangle, "Triangle 0 is degenerate.");
    }

    [Fact]
    public void Validate_NearZeroArea_IsDegenerate()
    {
        var triangle = CreateTriangle(
            new Vector3(0, 0, 0),
            new Vector3(1, 0, 0),
            new Vector3(0.5f, 0.0000001f, 0));

        var result = Validate(triangle);

        AssertIssue(result, ValidationIssueCodes.DegenerateTriangle, "Triangle 0 is degenerate.");
    }

    [Fact]
    public void Validate_SmallButWellProportionedMesh_IsNotDegenerate()
    {
        const float scale = 0.000000001f;
        var triangles = CreateTetrahedron()
            .Select(triangle => CreateTriangle(
                Scale(triangle.A, scale),
                Scale(triangle.B, scale),
                Scale(triangle.C, scale)))
            .ToArray();

        var result = Validate(triangles);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_SameTriangleWithDifferentVertexOrder_IsDuplicate()
    {
        var a = new Vector3(0, 0, 0);
        var b = new Vector3(1, 0, 0);
        var c = new Vector3(0, 1, 0);

        var result = Validate(
            CreateTriangle(a, b, c),
            CreateTriangle(c, a, b));

        Assert.Contains(result.Issues, issue =>
            issue.Code == ValidationIssueCodes.DuplicateTriangle &&
            issue.Message == "Triangle 1 duplicates triangle 0.");
    }

    [Fact]
    public void Validate_EdgeUsedByOneTriangle_IsOpen()
    {
        var result = Validate(CreateTriangle(
            new Vector3(0, 0, 0),
            new Vector3(1, 0, 0),
            new Vector3(0, 1, 0)));

        Assert.Equal(3, result.CountIssues(ValidationIssueCodes.OpenEdge));
        Assert.Equal(3, result.Topology.OpenEdgeCount);
        Assert.Equal(0, result.Topology.NonManifoldEdgeCount);
    }

    [Fact]
    public void Validate_EdgeUsedByMoreThanTwoTriangles_IsNonManifold()
    {
        var a = new Vector3(0, 0, 0);
        var b = new Vector3(1, 0, 0);

        var result = Validate(
            CreateTriangle(a, b, new Vector3(0, 1, 0)),
            CreateTriangle(b, a, new Vector3(0, 0, 1)),
            CreateTriangle(a, b, new Vector3(0, -1, 0)));

        Assert.Contains(result.Issues, issue =>
            issue.Code == ValidationIssueCodes.NonManifoldEdge &&
            issue.Message.Contains("is used by 3 triangles: 0, 1, 2."));
        Assert.Equal(6, result.Topology.OpenEdgeCount);
        Assert.Equal(1, result.Topology.NonManifoldEdgeCount);
    }

    [Fact]
    public void Validate_AdjacentTrianglesWithSameEdgeDirection_HaveInconsistentOrientation()
    {
        var a = new Vector3(0, 0, 0);
        var b = new Vector3(1, 0, 0);

        var result = Validate(
            CreateTriangle(a, b, new Vector3(0, 1, 0)),
            CreateTriangle(a, b, new Vector3(0, 0, 1)));

        Assert.Contains(result.Issues, issue =>
            issue.Code == ValidationIssueCodes.InconsistentOrientation &&
            issue.Message.StartsWith("Triangles 0 and 1 have inconsistent orientation"));
        Assert.True(result.Topology.HasInconsistentOrientation);
    }

    [Fact]
    public void Validate_AdjacentTrianglesWithOppositeEdgeDirections_HaveCompatibleOrientation()
    {
        var a = new Vector3(0, 0, 0);
        var b = new Vector3(1, 0, 0);

        var result = Validate(
            CreateTriangle(a, b, new Vector3(0, 1, 0)),
            CreateTriangle(b, a, new Vector3(0, 0, 1)));

        Assert.DoesNotContain(result.Issues, issue =>
            issue.Code == ValidationIssueCodes.InconsistentOrientation);
    }

    private static ValidationResult Validate(params Triangle[] triangles)
    {
        var mesh = new Mesh();
        mesh.Triangles.AddRange(triangles);
        return new MeshValidator().Validate(mesh);
    }

    private static void AssertIssue(ValidationResult result, string code, string message)
    {
        var issue = Assert.Single(result.Issues);
        Assert.Equal(code, issue.Code);
        Assert.Equal(message, issue.Message);
    }

    private static Triangle[] CreateTetrahedron(Vector3? offset = null)
    {
        var translation = offset ?? new Vector3(0, 0, 0);
        var a = translation;
        var b = new Vector3(translation.X + 1, translation.Y, translation.Z);
        var c = new Vector3(translation.X, translation.Y + 1, translation.Z);
        var d = new Vector3(translation.X, translation.Y, translation.Z + 1);

        return
        [
            CreateTriangle(a, c, b),
            CreateTriangle(a, b, d),
            CreateTriangle(a, d, c),
            CreateTriangle(b, c, d)
        ];
    }

    private static Triangle[] CreateTetrahedronTouchingOrigin()
    {
        var a = new Vector3(0, 0, 0);
        var b = new Vector3(-1, 0, 0);
        var c = new Vector3(0, -1, 0);
        var d = new Vector3(0, 0, -1);

        return
        [
            CreateTriangle(a, b, c),
            CreateTriangle(a, d, b),
            CreateTriangle(a, c, d),
            CreateTriangle(b, d, c)
        ];
    }

    private static Triangle CreateTriangle(Vector3 a, Vector3 b, Vector3 c)
    {
        return new Triangle(ZeroNormal, a, b, c);
    }

    private static Vector3 Scale(Vector3 vertex, float scale)
    {
        return new Vector3(vertex.X * scale, vertex.Y * scale, vertex.Z * scale);
    }
}
