using Processor.Models;
using Processor.Reporting;
using Processor.Validation;

namespace Processor.Tests.Reporting;

public class MeshQualityReportBuilderTests
{
    [Fact]
    public void Build_Tetrahedron_CalculatesFileAndGeometryMetadata()
    {
        var filePath = Path.GetTempFileName();

        try
        {
            File.WriteAllBytes(filePath, new byte[123]);
            var mesh = CreateUnitTetrahedron();

            var report = new MeshQualityReportBuilder().Build(
                filePath,
                mesh,
                new MeshValidator().Validate(mesh));

            Assert.Equal(Path.GetFileName(filePath), report.FileName);
            Assert.Equal(123, report.FileSizeBytes);
            Assert.Equal(4, report.TriangleCount);
            Assert.Equal(0, report.BoundingBox.MinX);
            Assert.Equal(0, report.BoundingBox.MinY);
            Assert.Equal(0, report.BoundingBox.MinZ);
            Assert.Equal(1, report.BoundingBox.MaxX);
            Assert.Equal(1, report.BoundingBox.MaxY);
            Assert.Equal(1, report.BoundingBox.MaxZ);
            Assert.Equal(1, report.Dimensions.Width);
            Assert.Equal(1, report.Dimensions.Height);
            Assert.Equal(1, report.Dimensions.Depth);
            Assert.Equal(1.5 + Math.Sqrt(3) / 2, report.SurfaceArea, 10);
            Assert.Equal(1.0 / 6.0, report.Volume, 10);
            Assert.Equal(1, report.Topology.ConnectedComponentCount);
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public void Build_InvalidCoordinate_ProducesFiniteSerializableMetadata()
    {
        var filePath = Path.GetTempFileName();

        try
        {
            var mesh = new Mesh();
            mesh.Triangles.Add(new Triangle(
                new Vector3(0, 0, 0),
                new Vector3(float.NaN, 0, 0),
                new Vector3(1, 0, 0),
                new Vector3(0, 1, 0)));

            var report = new MeshQualityReportBuilder().Build(
                filePath,
                mesh,
                new MeshValidator().Validate(mesh));
            var json = MeshQualityReportFormatter.FormatJson(report);

            Assert.Equal(0, report.SurfaceArea);
            Assert.Equal(0, report.Volume);
            Assert.Contains("\"invalid_coordinates\"", json);
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public void Build_OpenTriangle_IncludesBoundedOptionalHighlightSamples()
    {
        var filePath = Path.GetTempFileName();
        try
        {
            var mesh = new Mesh();
            mesh.Triangles.Add(new Triangle(
                new Vector3(0, 0, 1),
                new Vector3(0, 0, 0),
                new Vector3(1, 0, 0),
                new Vector3(0, 1, 0)));

            var report = new MeshQualityReportBuilder().Build(
                filePath, mesh, new MeshValidator().Validate(mesh));

            var samples = Assert.IsType<MeshIssueHighlights>(report.IssueHighlights).Samples;
            Assert.Equal(3, samples[ValidationIssueCodes.OpenEdge].Count);
            Assert.All(samples.Values, values => Assert.True(values.Count <= MeshIssueHighlights.SampleLimit));
            Assert.Contains("\"issueHighlights\"", MeshQualityReportFormatter.FormatJson(report));
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    private static Mesh CreateUnitTetrahedron()
    {
        var zero = new Vector3(0, 0, 0);
        var x = new Vector3(1, 0, 0);
        var y = new Vector3(0, 1, 0);
        var z = new Vector3(0, 0, 1);
        var normal = new Vector3(0, 0, 0);
        var mesh = new Mesh();
        mesh.Triangles.AddRange(
        [
            new Triangle(normal, zero, y, x),
            new Triangle(normal, zero, x, z),
            new Triangle(normal, zero, z, y),
            new Triangle(normal, x, y, z)
        ]);
        return mesh;
    }
}
