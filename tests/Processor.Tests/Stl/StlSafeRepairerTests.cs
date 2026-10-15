using Processor.Models;
using Processor.Stl;
using Processor.Validation;

namespace Processor.Tests.Stl;

public sealed class StlSafeRepairerTests
{
    [Fact]
    public void Removes_duplicate_and_degenerate_triangles_without_changing_tetrahedron_bounds()
    {
        var mesh = Tetrahedron();
        mesh.Triangles.Add(mesh.Triangles[0] with
        {
            B = mesh.Triangles[0].C,
            C = mesh.Triangles[0].B
        });
        mesh.Triangles.Add(new Triangle(new Vector3(0, 0, 0), new Vector3(0, 0, 0), new Vector3(0, 0, 0), new Vector3(1, 0, 0)));

        var result = new StlSafeRepairer().Repair(mesh);

        Assert.Equal(1, result.DuplicatesRemoved);
        Assert.Equal(1, result.DegeneratesRemoved);
        Assert.NotNull(result.CleanedMesh);
        Assert.Equal(4, result.CleanedMesh!.Triangles.Count);
        Assert.True(new MeshValidator().Validate(result.CleanedMesh).IsValid);
    }

    [Fact]
    public void Does_not_offer_output_for_valid_or_open_meshes()
    {
        var valid = new StlSafeRepairer().Repair(Tetrahedron());
        var open = Tetrahedron();
        open.Triangles.RemoveAt(0);

        Assert.Null(valid.CleanedMesh);
        Assert.Null(new StlSafeRepairer().Repair(open).CleanedMesh);
    }

    [Fact]
    public void Corrects_a_reversed_face_and_writes_binary_stl_with_normals()
    {
        var mesh = Tetrahedron();
        var face = mesh.Triangles[0];
        mesh.Triangles[0] = new Triangle(face.Normal, face.A, face.C, face.B);

        var result = new StlSafeRepairer().Repair(mesh);

        Assert.NotNull(result.CleanedMesh);
        Assert.Equal(1, result.OrientationsCorrected);
        Assert.True(new MeshValidator().Validate(result.CleanedMesh!).IsValid);
    }

    [Fact]
    public void Consistently_inward_closed_mesh_is_not_claimed_as_an_orientation_fix()
    {
        var mesh = Tetrahedron();
        for (var index = 0; index < mesh.Triangles.Count; index++)
        {
            var face = mesh.Triangles[index];
            mesh.Triangles[index] = new Triangle(face.Normal, face.A, face.C, face.B);
        }

        var result = new StlSafeRepairer().Repair(mesh);

        Assert.Equal(0, result.OrientationsCorrected);
        Assert.Null(result.CleanedMesh);
        Assert.False(new MeshValidator().Validate(mesh).Topology.HasInconsistentOrientation);
    }

    private static Mesh Tetrahedron()
    {
        var a = new Vector3(0, 0, 0);
        var b = new Vector3(1, 0, 0);
        var c = new Vector3(0, 1, 0);
        var d = new Vector3(0, 0, 1);
        var mesh = new Mesh();
        mesh.Triangles.AddRange([
            new Triangle(new Vector3(0, 0, -1), a, c, b),
            new Triangle(new Vector3(0, -1, 0), a, b, d),
            new Triangle(new Vector3(-1, 0, 0), a, d, c),
            new Triangle(new Vector3(1, 1, 1), b, c, d)
        ]);
        return mesh;
    }
}
