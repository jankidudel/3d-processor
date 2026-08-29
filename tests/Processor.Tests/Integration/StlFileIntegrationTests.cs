using Processor.Stl;
using Processor.Validation;

namespace Processor.Tests.Integration;

public class StlFileIntegrationTests
{
    [Theory]
    [Trait("Category", "Integration")]
    [InlineData("releaser.stl", 1246)]
    [InlineData("rotor.stl", 2356)]
    public void BinaryStl_ReadAndValidate_ProducesExpectedValidMesh(
        string fileName,
        int expectedTriangleCount)
    {
        var filePath = GetFixturePath(fileName);

        var mesh = new BinaryStlReader().Read(filePath);
        var validationResult = new MeshValidator().Validate(mesh);

        Assert.Equal(expectedTriangleCount, mesh.Triangles.Count);
        Assert.True(
            validationResult.IsValid,
            $"Expected {fileName} to be valid, but validation reported: " +
            string.Join(" | ", validationResult.Issues.Take(10).Select(issue => issue.Message)));
    }

    [Theory]
    [Trait("Category", "Integration")]
    [InlineData("degenerate", "rotor-degenerate.stl", 2356, ValidationIssueCodes.DegenerateTriangle, 1)]
    [InlineData("duplicate-triangles", "rotor-duplicate-triangle.stl", 2356, ValidationIssueCodes.DuplicateTriangle, 1)]
    [InlineData("inconsistent-orientation", "rotor-inconsistent-orientation.stl", 2356, ValidationIssueCodes.InconsistentOrientation, 3)]
    [InlineData("invalid-coordinates", "rotor-invalid-coordinate.stl", 2356, ValidationIssueCodes.InvalidCoordinates, 1)]
    [InlineData("open-edges", "rotor-open-edges.stl", 2355, ValidationIssueCodes.OpenEdge, 3)]
    [InlineData("non-manifold", "rotor-non-manifold.stl", 2357, ValidationIssueCodes.NonManifoldEdge, 3)]
    public void BinaryStl_ReadAndValidate_DetectsExpectedIssue(
        string directoryName,
        string fileName,
        int expectedTriangleCount,
        string expectedIssueCode,
        int expectedIssueCount)
    {
        var filePath = GetFixturePath(directoryName, fileName);

        var mesh = new BinaryStlReader().Read(filePath);
        var validationResult = new MeshValidator().Validate(mesh);

        Assert.Equal(expectedTriangleCount, mesh.Triangles.Count);
        Assert.False(validationResult.IsValid);
        Assert.Equal(expectedIssueCount, validationResult.CountIssues(expectedIssueCode));
    }

    [Theory]
    [Trait("Category", "Integration")]
    [InlineData("one-connected-shell.stl", 4, 1, true)]
    [InlineData("two-disconnected-closed-shells.stl", 8, 2, true)]
    [InlineData("three-disconnected-shells.stl", 12, 3, true)]
    [InlineData("two-shells-touching-at-one-vertex.stl", 8, 1, true)]
    public void AsciiStl_ReadAndValidate_ReportsExpectedConnectedComponents(
        string fileName,
        int expectedTriangleCount,
        int expectedComponentCount,
        bool expectedValid)
    {
        var filePath = GetFixturePath("connected-components", fileName);

        var mesh = new AsciiStlReader().Read(filePath);
        var validationResult = new MeshValidator().Validate(mesh);

        Assert.Equal(expectedTriangleCount, mesh.Triangles.Count);
        Assert.Equal(expectedComponentCount, validationResult.Topology.ConnectedComponentCount);
        Assert.Equal(expectedValid, validationResult.IsValid);
        Assert.Equal(
            expectedComponentCount > 1 ? expectedComponentCount : 0,
            validationResult.CountIssues(ValidationIssueCodes.MultipleDisconnectedShells));
    }

    private static string GetFixturePath(string fileName)
    {
        return GetFixturePath("valid", fileName);
    }

    private static string GetFixturePath(string directoryName, string fileName)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "3d-processor.slnx")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);

        var filePath = Path.Combine(
            directory.FullName,
            "tests",
            "TestData",
            "stl",
            directoryName,
            fileName);
        Assert.True(File.Exists(filePath), $"Integration fixture was not found: {filePath}");
        return filePath;
    }
}
