using System.Text.Json;
using Processor.Models;
using Processor.Reporting;
using Processor.ThreeMf;
using Processor.Validation;

namespace Processor.Tests.Reporting;

public class UnifiedMeshAnalysisTests
{
    [Fact]
    public void Validate_TriangleInYZPlane_HighlightsMatchAuthoritativeTopology()
    {
        // A valid YZ triangle was incorrectly classified as degenerate by the
        // old independent highlight pass (its cross-product X term was wrong).
        var mesh = new Mesh();
        mesh.Triangles.Add(new Triangle(default,
            new Vector3(1, 0, 0), new Vector3(1, 1, 0), new Vector3(1, 0, 1)));

        var validation = new MeshValidator().Validate(mesh);
        var analysis = validation.IssueAnalysis!;

        Assert.Equal(0, validation.CountIssues(ValidationIssueCodes.DegenerateTriangle));
        Assert.False(analysis.Highlights.Samples.ContainsKey(ValidationIssueCodes.DegenerateTriangle));
        Assert.Equal(3, validation.Topology.OpenEdgeCount);
        Assert.Equal(validation.Topology.OpenEdgeCount, analysis.Geometry.OpenEdges!.TotalCount);
        Assert.Equal(3, analysis.Highlights.Samples[ValidationIssueCodes.OpenEdge].Count);
    }

    [Fact]
    public void Validate_ManyDegenerateTriangles_PreservesCountWithBoundedExamples()
    {
        var mesh = new Mesh();
        for (var i = 0; i < 10_000; i++)
        {
            mesh.Triangles.Add(new Triangle(default, default, default, default));
        }

        var validation = new MeshValidator().Validate(mesh);

        Assert.False(validation.IsValid);
        Assert.Equal(10_000, validation.CountIssues(ValidationIssueCodes.DegenerateTriangle));
        Assert.Equal(ValidationIssueCollector.ExampleLimit + 1, validation.Issues.Count);
        Assert.Equal(10_000 - ValidationIssueCollector.ExampleLimit, validation.Issues[^1].Count);
        Assert.Equal(MeshIssueHighlights.SampleLimit,
            validation.IssueAnalysis!.Highlights.Samples[ValidationIssueCodes.DegenerateTriangle].Count);
        Assert.Equal(0, validation.IssueAnalysis.Geometry.OpenEdges!.TotalCount);
    }

    [Fact]
    public void Validate_ThousandsOfUsesOfOneEdge_KeepsExactCountsWithoutPerEdgeLists()
    {
        var mesh = new Mesh();
        for (var i = 0; i < 10_000; i++)
        {
            mesh.Triangles.Add(new Triangle(default,
                new Vector3(0, 0, 0), new Vector3(1, 0, 0), new Vector3(0, 1, 0)));
        }

        var validation = new MeshValidator().Validate(mesh);

        Assert.Equal(9_999, validation.CountIssues(ValidationIssueCodes.DuplicateTriangle));
        Assert.Equal(3, validation.Topology.NonManifoldEdgeCount);
        Assert.Equal(3, validation.IssueAnalysis!.Geometry.NonManifoldEdges!.TotalCount);
        Assert.All(validation.Issues.Where(issue => issue.Code == ValidationIssueCodes.NonManifoldEdge),
            issue => Assert.Contains("10000 triangles: 0, 1, 2, ...", issue.Message));
    }

    [Fact]
    public void Build_ReusesValidationAnalysis_WithoutAddingInternalFieldsToJson()
    {
        var path = Path.GetTempFileName();
        try
        {
            var mesh = new Mesh();
            mesh.Triangles.Add(new Triangle(default,
                new Vector3(1, 0, 0), new Vector3(1, 1, 0), new Vector3(1, 0, 1)));
            var validation = new MeshValidator().Validate(mesh);
            var report = new MeshQualityReportBuilder().Build(path, mesh, validation);

            Assert.Same(validation.IssueAnalysis!.Highlights, report.IssueHighlights);
            Assert.Same(validation.IssueAnalysis.Geometry, report.IssueGeometry);
            Assert.Equal(0.5, report.SurfaceArea);
            using var json = JsonDocument.Parse(MeshQualityReportFormatter.FormatJson(report));
            var jsonValidation = json.RootElement.GetProperty("validation");
            Assert.False(jsonValidation.TryGetProperty("issueAnalysis", out _));
            Assert.False(jsonValidation.TryGetProperty("geometry", out _));
            Assert.Equal(1, json.RootElement.GetProperty("issueHighlights")
                .GetProperty("samples").GetProperty("open_edge")[0].GetProperty("a").GetProperty("x").GetSingle());

            var withoutHighlights = new MeshQualityReportBuilder().Build(
                path, mesh, validation, includeIssueHighlights: false);
            Assert.Null(withoutHighlights.IssueHighlights);
            Assert.Null(withoutHighlights.IssueGeometry);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void ThreeMf_CoincidentBuildItems_AggregateBoundedDiagnosticsWithoutJoiningEdges()
    {
        var meshes = new List<Mesh>();
        for (var i = 0; i < 40; i++)
        {
            var mesh = new Mesh();
            mesh.Triangles.Add(new Triangle(default,
                new Vector3(1, 0, 0), new Vector3(1, 1, 0), new Vector3(1, 0, 1)));
            meshes.Add(mesh);
        }

        var result = ThreeMfValidationAggregator.ValidateBuildItems(meshes);

        Assert.Equal(40, result.Topology.ConnectedComponentCount);
        Assert.Equal(120, result.Topology.OpenEdgeCount);
        Assert.Equal(120, result.IssueAnalysis!.Geometry.OpenEdges!.TotalCount);
        Assert.Equal(MeshIssueHighlights.SampleLimit,
            result.IssueAnalysis.Highlights.Samples[ValidationIssueCodes.OpenEdge].Count);
        Assert.Equal(0, result.IssueAnalysis.Geometry.NonManifoldEdges!.TotalCount);
    }
}
