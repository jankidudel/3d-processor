using System.Text;
using Processor.Stl;

namespace Processor.Tests.Stl;

public class StlReaderTests
{
    [Theory]
    [InlineData("validation-failure-open-triangle.stl", 1)]
    [InlineData("validation-success-closed-tetrahedron.stl", 4)]
    [InlineData("validation-failure-open-triangle-whitespace-scientific.stl", 1)]
    public void Read_ValidAsciiFixture_ParsesTriangles(string fileName, int triangleCount)
    {
        var mesh = new StlReader().Read(GetFixturePath("ascii", fileName));

        Assert.Equal(triangleCount, mesh.Triangles.Count);
    }

    [Fact]
    public void Read_AsciiWithCrLfTabsAndScientificNotation_ParsesCoordinates()
    {
        var path = WriteTemporary(
            "solid crlf\r\n\tfacet normal 0 0 1e0\r\n\touter loop\r\n" +
            "vertex 0 0 0\r\nvertex 1e0 0 0\r\nvertex 0 1E+0 0\r\n" +
            "endloop\r\nendfacet\r\nendsolid crlf\r\n");
        try
        {
            var triangle = Assert.Single(new StlReader().Read(path).Triangles);
            Assert.Equal(1, triangle.B.X);
            Assert.Equal(1, triangle.C.Y);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Theory]
    [InlineData("solid x\nfacet normal 0 0 1\nouter loop\nvertex 0 0 0\nvertex 1 0 0\nendloop\nendfacet\nendsolid x\n")]
    [InlineData("solid x\nfacet normal 0 0 1\nouter loop\nvertex 0 0 0\nvertex 1 0 0\nvertex 0 1 0\nendloop\nendsolid x\n")]
    [InlineData("solid x\nfacet normal 0 0 1\nouter loop\nvertex nope 0 0\nvertex 1 0 0\nvertex 0 1 0\nendloop\nendfacet\nendsolid x\n")]
    public void Read_MalformedAscii_ThrowsTypedError(string contents)
    {
        var path = WriteTemporary(contents);
        try
        {
            Assert.Throws<MalformedAsciiStlException>(() => new StlReader().Read(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Read_BinaryWhoseHeaderStartsWithSolid_RemainsBinary()
    {
        var path = WriteBinaryTetrahedron(header: "solid binary header");
        try
        {
            Assert.Equal(4, new StlReader().Read(path).Triangles.Count);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Process_EquivalentAsciiAndBinaryGeometry_ProducesEquivalentReport()
    {
        var asciiPath = GetFixturePath(
            "ascii",
            "validation-success-closed-tetrahedron.stl");
        var binaryPath = WriteBinaryTetrahedron();
        try
        {
            var ascii = new ProcessorApplication().Process(asciiPath);
            var binary = new ProcessorApplication().Process(binaryPath);

            Assert.Equal(binary.TriangleCount, ascii.TriangleCount);
            Assert.Equal(binary.BoundingBox.MinX, ascii.BoundingBox.MinX);
            Assert.Equal(binary.BoundingBox.MinY, ascii.BoundingBox.MinY);
            Assert.Equal(binary.BoundingBox.MinZ, ascii.BoundingBox.MinZ);
            Assert.Equal(binary.BoundingBox.MaxX, ascii.BoundingBox.MaxX);
            Assert.Equal(binary.BoundingBox.MaxY, ascii.BoundingBox.MaxY);
            Assert.Equal(binary.BoundingBox.MaxZ, ascii.BoundingBox.MaxZ);
            Assert.Equal(binary.Dimensions.Width, ascii.Dimensions.Width);
            Assert.Equal(binary.Dimensions.Height, ascii.Dimensions.Height);
            Assert.Equal(binary.Dimensions.Depth, ascii.Dimensions.Depth);
            Assert.Equal(binary.SurfaceArea, ascii.SurfaceArea, 10);
            Assert.Equal(binary.Volume!.Value, ascii.Volume!.Value, 10);
            Assert.Equal(binary.Validation.IsValid, ascii.Validation.IsValid);
            Assert.Equal(binary.Validation.Issues, ascii.Validation.Issues);
        }
        finally
        {
            File.Delete(binaryPath);
        }
    }

    [Fact]
    public void Read_UnrecognizedContent_ThrowsTypedError()
    {
        var path = WriteTemporary("not an stl");
        try
        {
            Assert.Throws<UnrecognizedStlFormatException>(() => new StlReader().Read(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Read_TruncatedBinary_ThrowsTypedError()
    {
        Assert.Throws<MalformedBinaryStlException>(() =>
            new StlReader().Read(GetFixturePath("corrupt", "rotor-truncated.stl")));
    }

    private static string WriteTemporary(string contents)
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.stl");
        File.WriteAllText(path, contents, new UTF8Encoding(false));
        return path;
    }

    private static string WriteBinaryTetrahedron(string header = "binary tetrahedron")
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.stl");
        using var stream = File.Create(path);
        using var writer = new BinaryWriter(stream);
        var headerBytes = new byte[80];
        Encoding.ASCII.GetBytes(header).CopyTo(headerBytes, 0);
        writer.Write(headerBytes);
        writer.Write(4u);
        WriteTriangle(writer, (0, 0, 0), (0, 1, 0), (1, 0, 0));
        WriteTriangle(writer, (0, 0, 0), (1, 0, 0), (0, 0, 1));
        WriteTriangle(writer, (1, 0, 0), (0, 1, 0), (0, 0, 1));
        WriteTriangle(writer, (0, 1, 0), (0, 0, 0), (0, 0, 1));
        return path;
    }

    private static void WriteTriangle(
        BinaryWriter writer,
        (float X, float Y, float Z) a,
        (float X, float Y, float Z) b,
        (float X, float Y, float Z) c)
    {
        WriteVector(writer, (0, 0, 0));
        WriteVector(writer, a);
        WriteVector(writer, b);
        WriteVector(writer, c);
        writer.Write((ushort)0);
    }

    private static void WriteVector(BinaryWriter writer, (float X, float Y, float Z) value)
    {
        writer.Write(value.X);
        writer.Write(value.Y);
        writer.Write(value.Z);
    }

    private static string GetFixturePath(string directoryName, string fileName)
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
