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
        var validationResult = new StlValidator().Validate(mesh);

        Assert.Equal(expectedTriangleCount, mesh.Triangles.Count);
        Assert.True(
            validationResult.IsValid,
            $"Expected {fileName} to be valid, but validation reported: " +
            string.Join(" | ", validationResult.Errors.Take(10)));
    }

    private static string GetFixturePath(string fileName)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "3d-processor.slnx")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);

        var filePath = Path.Combine(directory.FullName, "data", "valid", fileName);
        Assert.True(File.Exists(filePath), $"Integration fixture was not found: {filePath}");
        return filePath;
    }
}
