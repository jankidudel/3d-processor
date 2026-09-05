using Processor.Stl;
using Processor.Validation;

namespace Processor.Tests.Stl;

public class StlSplitterTests
{
    [Theory]
    [InlineData(SplitAxis.X)]
    [InlineData(SplitAxis.Y)]
    [InlineData(SplitAxis.Z)]
    public void Split_ClosedTetrahedronAtMidpoint_ProducesTwoValidCappedParts(SplitAxis axis)
    {
        var mesh = new StlReader().Read(FixturePath("ascii", "validation-success-closed-tetrahedron.stl"));

        var result = new StlSplitter().Split(mesh, axis, 0.5);
        var validator = new MeshValidator();

        Assert.NotEmpty(result.PartA.Triangles);
        Assert.NotEmpty(result.PartB.Triangles);
        Assert.True(validator.Validate(result.PartA).IsValid);
        Assert.True(validator.Validate(result.PartB).IsValid);
    }

    private static string FixturePath(string directoryName, string fileName)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "3d-processor.slnx")))
        {
            directory = directory.Parent;
        }
        Assert.NotNull(directory);
        return Path.Combine(directory.FullName, "tests", "TestData", "stl", directoryName, fileName);
    }
}
