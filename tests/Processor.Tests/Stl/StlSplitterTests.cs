using Processor.Stl;
using Processor.Validation;
using Processor.Models;

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

    [Fact]
    public void Split_ShiftedCubeAtMidpoint_PreservesSourceCoordinates()
    {
        var mesh = ClosedBox(20, 20, 20, 0, 0, -5);

        var result = new StlSplitter().Split(mesh, SplitAxis.Z, 5);

        Assert.Equal((-5, 5), ZBounds(result.PartA));
        Assert.Equal((5, 15), ZBounds(result.PartB));
        Assert.True(new MeshValidator().Validate(result.PartA).IsValid);
        Assert.True(new MeshValidator().Validate(result.PartB).IsValid);
    }

    [Fact]
    public void SplitStl_ClosedTubeAtMidpoint_RejectsInvalidCapsAndRemovesOutputs()
    {
        var mesh = ClosedRectangularTube();
        var validation = new MeshValidator().Validate(mesh);

        Assert.True(validation.IsValid);
        var input = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}-tube.stl");
        var lower = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}-lower.stl");
        var upper = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}-upper.stl");
        try
        {
            BinaryStlWriter.Write(input, mesh.Triangles);
            var error = Assert.Throws<SplitException>(() =>
                new ProcessorApplication().SplitStl(input, "tube.stl", SplitAxis.Z, 50, lower, upper));
            Assert.Contains("could not be capped and validated", error.Message);
            Assert.False(File.Exists(lower));
            Assert.False(File.Exists(upper));
        }
        finally
        {
            File.Delete(input);
            File.Delete(lower);
            File.Delete(upper);
        }
    }

    [Fact]
    public void SplitStl_ClosedCube_ReopensBothOutputsWithExpectedBounds()
    {
        var inputPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}-cube.stl");
        var partAPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}-lower-z.stl");
        var partBPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}-upper-z.stl");
        try
        {
            BinaryStlWriter.Write(inputPath, ClosedBox(20, 20, 20).Triangles);

            var report = new ProcessorApplication().SplitStl(
                inputPath,
                "closed-20-unit-cube.stl",
                SplitAxis.Z,
                50,
                partAPath,
                partBPath);

            Assert.Equal(10, report.Split.PositionCoordinate);
            Assert.Equal(0, report.Split.PartA.BoundingBox.MinZ);
            Assert.Equal(10, report.Split.PartA.BoundingBox.MaxZ);
            Assert.Equal(10, report.Split.PartB.BoundingBox.MinZ);
            Assert.Equal(20, report.Split.PartB.BoundingBox.MaxZ);
            Assert.Equal(10, report.Split.PartA.Dimensions.Depth);
            Assert.Equal(10, report.Split.PartB.Dimensions.Depth);
            Assert.True(report.Split.PartA.Validation.IsValid);
            Assert.True(report.Split.PartB.Validation.IsValid);
            Assert.NotEmpty(new StlReader().Read(partAPath).Triangles);
            Assert.NotEmpty(new StlReader().Read(partBPath).Triangles);
        }
        finally
        {
            File.Delete(inputPath);
            File.Delete(partAPath);
            File.Delete(partBPath);
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(100)]
    public void SplitStl_EndpointPercentage_RejectsWithoutOutputs(double percentage)
    {
        var input = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}-cube.stl");
        var lower = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}-lower.stl");
        var upper = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}-upper.stl");
        try
        {
            BinaryStlWriter.Write(input, ClosedBox(20, 20, 20).Triangles);
            Assert.Throws<SplitException>(() =>
                new ProcessorApplication().SplitStl(input, "cube.stl", SplitAxis.Z, percentage, lower, upper));
            Assert.False(File.Exists(lower));
            Assert.False(File.Exists(upper));
        }
        finally
        {
            File.Delete(input);
            File.Delete(lower);
            File.Delete(upper);
        }
    }

    [Fact]
    public void Split_DisjointContours_CanRetainMultipleComponentsPerExport()
    {
        var mesh = ClosedBox(20, 20, 20);
        mesh.Triangles.AddRange(ClosedBox(20, 20, 20, 40).Triangles);
        var result = new StlSplitter().Split(mesh, SplitAxis.Z, 10);
        foreach (var part in new[] { result.PartA, result.PartB })
        {
            var validation = new MeshValidator().Validate(part);
            Assert.True(validation.IsValid);
            Assert.Equal(2, validation.Topology.ConnectedComponentCount);
        }
    }

    private static Mesh ClosedBox(
        float width,
        float depth,
        float height,
        float x = 0,
        float y = 0,
        float z = 0)
    {
        var vertices = new[]
        {
            new Vector3(x, y, z),
            new Vector3(x + width, y, z),
            new Vector3(x + width, y + depth, z),
            new Vector3(x, y + depth, z),
            new Vector3(x, y, z + height),
            new Vector3(x + width, y, z + height),
            new Vector3(x + width, y + depth, z + height),
            new Vector3(x, y + depth, z + height)
        };
        var faces = new[]
        {
            0, 2, 1, 0, 3, 2,
            4, 5, 6, 4, 6, 7,
            0, 1, 5, 0, 5, 4,
            3, 7, 6, 3, 6, 2,
            0, 4, 7, 0, 7, 3,
            1, 2, 6, 1, 6, 5
        };
        return MeshFromFaces(vertices, faces);
    }

    private static Mesh ClosedRectangularTube()
    {
        var mesh = new Mesh();
        AddRing(mesh, 0, false);
        AddRing(mesh, 20, true);
        AddWallLoop(mesh, new[]
        {
            new Vector3(0, 0, 0),
            new Vector3(20, 0, 0),
            new Vector3(20, 20, 0),
            new Vector3(0, 20, 0)
        }, 20, false);
        AddWallLoop(mesh, new[]
        {
            new Vector3(4, 4, 0),
            new Vector3(4, 16, 0),
            new Vector3(16, 16, 0),
            new Vector3(16, 4, 0)
        }, 20, false);
        return mesh;
    }

    private static void AddRing(Mesh mesh, float z, bool upward)
    {
        var outer = new[]
        {
            new Vector3(0, 0, z),
            new Vector3(20, 0, z),
            new Vector3(20, 20, z),
            new Vector3(0, 20, z)
        };
        var inner = new[]
        {
            new Vector3(4, 4, z),
            new Vector3(16, 4, z),
            new Vector3(16, 16, z),
            new Vector3(4, 16, z)
        };
        for (var index = 0; index < 4; index++)
        {
            var next = (index + 1) % 4;
            if (upward)
            {
                AddQuad(mesh, outer[index], outer[next], inner[next], inner[index]);
            }
            else
            {
                AddQuad(mesh, outer[next], outer[index], inner[index], inner[next]);
            }
        }
    }

    private static void AddWallLoop(
        Mesh mesh,
        IReadOnlyList<Vector3> bottom,
        float height,
        bool reverse)
    {
        for (var index = 0; index < bottom.Count; index++)
        {
            var next = (index + 1) % bottom.Count;
            var a = bottom[index];
            var b = bottom[next];
            var c = b with
            {
                Z = b.Z + height
            };
            var d = a with
            {
                Z = a.Z + height
            };
            if (reverse)
            {
                AddQuad(mesh, b, a, d, c);
            }
            else
            {
                AddQuad(mesh, a, b, c, d);
            }
        }
    }

    private static void AddQuad(Mesh mesh, Vector3 a, Vector3 b, Vector3 c, Vector3 d)
    {
        mesh.Triangles.Add(TriangleFrom(a, b, c));
        mesh.Triangles.Add(TriangleFrom(a, c, d));
    }

    private static Mesh MeshFromFaces(IReadOnlyList<Vector3> vertices, IReadOnlyList<int> faces)
    {
        var mesh = new Mesh();
        for (var index = 0; index < faces.Count; index += 3)
        {
            mesh.Triangles.Add(TriangleFrom(
                vertices[faces[index]],
                vertices[faces[index + 1]],
                vertices[faces[index + 2]]));
        }
        return mesh;
    }

    private static Triangle TriangleFrom(Vector3 a, Vector3 b, Vector3 c)
    {
        var ab = new Vector3(b.X - a.X, b.Y - a.Y, b.Z - a.Z);
        var ac = new Vector3(c.X - a.X, c.Y - a.Y, c.Z - a.Z);
        return new Triangle(
            new Vector3(
                ab.Y * ac.Z - ab.Z * ac.Y,
                ab.Z * ac.X - ab.X * ac.Z,
                ab.X * ac.Y - ab.Y * ac.X),
            a,
            b,
            c);
    }

    private static (float Minimum, float Maximum) ZBounds(Mesh mesh)
    {
        var values = mesh.Triangles
            .SelectMany(triangle => new[] { triangle.A.Z, triangle.B.Z, triangle.C.Z })
            .ToArray();
        return (values.Min(), values.Max());
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
