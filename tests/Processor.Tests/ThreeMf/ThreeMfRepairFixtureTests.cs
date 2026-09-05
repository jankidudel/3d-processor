using System.IO.Compression;
using System.Xml.Linq;
using Processor.Reporting;
using Processor.ThreeMf;
using Processor.Validation;

namespace Processor.Tests.ThreeMf;

public sealed class ThreeMfRepairFixtureTests
{
    [Theory]
    [MemberData(nameof(Fixtures))]
    public void RepairFixture_ReportsExpectedValidationAndRepairOutcome(
        RepairFixtureExpectation expectation)
    {
        var outputPath = Path.Combine(
            Path.GetTempPath(),
            $"{Path.GetFileNameWithoutExtension(expectation.FileName)}-{Guid.NewGuid():N}.3mf");
        try
        {
            var report = new ProcessorApplication().RepairThreeMf(
                FixturePath(expectation.FileName),
                expectation.FileName,
                outputPath);

            Assert.Equal(expectation.TrianglesBefore, report.BeforeValidation.TriangleCount);
            Assert.Equal(
                expectation.BeforeIssueCount,
                report.BeforeValidation.Validation.CountIssues(expectation.BeforeIssueCode));
            Assert.Equal(expectation.TrianglesBefore, report.Repair.TrianglesBefore);
            Assert.Equal(expectation.TrianglesAfter, report.Repair.TrianglesAfter);
            Assert.Equal(
                expectation.DegenerateTrianglesRemoved,
                report.Repair.DegenerateTrianglesRemoved);
            Assert.Equal(
                expectation.DuplicateTrianglesRemoved,
                report.Repair.DuplicateTrianglesRemoved);
            Assert.Equal(
                expectation.OrientationFixesApplied,
                report.Repair.OrientationFixesApplied);
            Assert.Equal(expectation.AfterIsValid, report.AfterValidation!.Validation.IsValid);
            Assert.Equal(
                expectation.AfterIssueCount,
                report.AfterValidation.Validation.CountIssues(expectation.AfterIssueCode));
            Assert.Equal(expectation.CreatesOutput, report.Repair.RepairedFileAvailable);
            Assert.Equal(expectation.CreatesOutput, File.Exists(outputPath));

            if (expectation.CreatesOutput)
            {
                var reopened = new ThreeMfReader().Read(outputPath);
                Assert.Equal(expectation.TrianglesAfter, reopened.Mesh.Triangles.Count);
            }
        }
        finally
        {
            File.Delete(outputPath);
        }
    }

    [Fact]
    public void MultiObjectFixture_RepairsOnlyAffectedMeshAndPreservesPackageStructure()
    {
        var sourcePath = FixturePath("multi-object-with-metadata.3mf");
        var outputPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.3mf");
        try
        {
            var sourceDocument = ReadModelDocument(sourcePath);
            var sourceObjectOne = sourceDocument.Descendants()
                .Single(element =>
                    element.Name.LocalName == "object" &&
                    (string?)element.Attribute("id") == "1")
                .ToString(SaveOptions.DisableFormatting);
            var report = new ProcessorApplication().RepairThreeMf(
                sourcePath,
                Path.GetFileName(sourcePath),
                outputPath);

            Assert.True(report.Repair.RepairedFileAvailable);
            Assert.Equal(25, report.Repair.TrianglesBefore);
            Assert.Equal(24, report.Repair.TrianglesAfter);
            Assert.Equal(1, report.Repair.DuplicateTrianglesRemoved);
            Assert.True(report.AfterValidation!.Validation.IsValid);

            using var archive = ZipFile.OpenRead(outputPath);
            var modelEntry = archive.GetEntry("3D/model.model");
            Assert.NotNull(modelEntry);
            using var stream = modelEntry.Open();
            var document = XDocument.Load(stream);
            var root = document.Root;
            Assert.NotNull(root);
            Assert.Equal("millimeter", (string?)root.Attribute("unit"));
            Assert.Contains(
                root.Elements().Where(element => element.Name.LocalName == "metadata"),
                element => element.Value == "multi-object metadata retained");
            Assert.Single(root.Descendants(), element =>
                element.Name.LocalName == "basematerials" &&
                (string?)element.Attribute("id") == "5");
            Assert.Equal(
                ["1", "2"],
                root.Descendants()
                    .Where(element => element.Name.LocalName == "object")
                    .Select(element => (string)element.Attribute("id")!)
                    .ToArray());
            Assert.Equal(
                ["1", "2"],
                root.Descendants()
                    .Where(element => element.Name.LocalName == "item")
                    .Select(element => (string)element.Attribute("objectid")!)
                    .ToArray());

            var objects = root.Descendants()
                .Where(element => element.Name.LocalName == "object")
                .ToDictionary(element => (string)element.Attribute("id")!);
            Assert.Equal(12, TriangleCount(objects["1"]));
            Assert.Equal(12, TriangleCount(objects["2"]));
            Assert.Equal(
                sourceObjectOne,
                objects["1"].ToString(SaveOptions.DisableFormatting));
        }
        finally
        {
            File.Delete(outputPath);
        }
    }

    [Fact]
    public void CombinedFixture_RemovesSafeIssuesAndRetainsOpenEdges()
    {
        var outputPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.3mf");
        try
        {
            var report = new ProcessorApplication().RepairThreeMf(
                FixturePath("combined-safe-and-unsafe-issues.3mf"),
                "combined-safe-and-unsafe-issues.3mf",
                outputPath);

            Assert.Equal(
                2,
                report.BeforeValidation.Validation.CountIssues(
                    ValidationIssueCodes.OpenEdge));
            Assert.Equal(
                1,
                report.BeforeValidation.Validation.CountIssues(
                    ValidationIssueCodes.DuplicateTriangle));
            Assert.Equal(
                1,
                report.BeforeValidation.Validation.CountIssues(
                    ValidationIssueCodes.DegenerateTriangle));
            Assert.Equal(
                3,
                report.AfterValidation!.Validation.CountIssues(
                    ValidationIssueCodes.OpenEdge));
            Assert.Equal(
                0,
                report.AfterValidation.Validation.CountIssues(
                    ValidationIssueCodes.DuplicateTriangle));
            Assert.Equal(
                0,
                report.AfterValidation.Validation.CountIssues(
                    ValidationIssueCodes.DegenerateTriangle));
        }
        finally
        {
            File.Delete(outputPath);
        }
    }

    public static IEnumerable<object[]> Fixtures()
    {
        yield return [new RepairFixtureExpectation(
            "valid-closed-cube.3mf",
            ValidationIssueCodes.OpenEdge,
            0,
            12,
            12,
            0,
            0,
            0,
            true,
            false)];
        yield return [new RepairFixtureExpectation(
            "degenerate-triangles.3mf",
            ValidationIssueCodes.DegenerateTriangle,
            1,
            13,
            12,
            1,
            0,
            0,
            true,
            true)];
        yield return [new RepairFixtureExpectation(
            "duplicate-triangles.3mf",
            ValidationIssueCodes.DuplicateTriangle,
            1,
            13,
            12,
            0,
            1,
            0,
            true,
            true)];
        yield return [new RepairFixtureExpectation(
            "flipped-orientation.3mf",
            ValidationIssueCodes.InconsistentOrientation,
            3,
            12,
            12,
            0,
            0,
            11,
            true,
            true)];
        yield return [new RepairFixtureExpectation(
            "open-mesh.3mf",
            ValidationIssueCodes.OpenEdge,
            3,
            11,
            11,
            0,
            0,
            0,
            false,
            false,
            ValidationIssueCodes.OpenEdge,
            3)];
        yield return [new RepairFixtureExpectation(
            "non-manifold-edge.3mf",
            ValidationIssueCodes.NonManifoldEdge,
            2,
            13,
            13,
            0,
            0,
            0,
            false,
            false,
            ValidationIssueCodes.NonManifoldEdge,
            2)];
        yield return [new RepairFixtureExpectation(
            "combined-safe-and-unsafe-issues.3mf",
            ValidationIssueCodes.DegenerateTriangle,
            1,
            13,
            11,
            1,
            1,
            0,
            false,
            true,
            ValidationIssueCodes.OpenEdge,
            3)];
        yield return [new RepairFixtureExpectation(
            "multi-object-with-metadata.3mf",
            ValidationIssueCodes.DuplicateTriangle,
            1,
            25,
            24,
            0,
            1,
            0,
            true,
            true)];
    }

    private static int TriangleCount(XElement element)
    {
        return element.Descendants().Count(value => value.Name.LocalName == "triangle");
    }

    private static XDocument ReadModelDocument(string filePath)
    {
        using var archive = ZipFile.OpenRead(filePath);
        var modelEntry = archive.GetEntry("3D/model.model");
        Assert.NotNull(modelEntry);
        using var stream = modelEntry.Open();
        return XDocument.Load(stream);
    }

    private static string FixturePath(string fileName)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null &&
               !File.Exists(Path.Combine(directory.FullName, "3d-processor.slnx")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        var path = Path.Combine(
            directory.FullName,
            "tests",
            "TestData",
            "3mf",
            "repair",
            fileName);
        Assert.True(File.Exists(path), $"3MF repair fixture was not found: {path}");
        return path;
    }

    public sealed record RepairFixtureExpectation(
        string FileName,
        string BeforeIssueCode,
        int BeforeIssueCount,
        int TrianglesBefore,
        int TrianglesAfter,
        int DegenerateTrianglesRemoved,
        int DuplicateTrianglesRemoved,
        int OrientationFixesApplied,
        bool AfterIsValid,
        bool CreatesOutput,
        string AfterIssueCode = ValidationIssueCodes.OpenEdge,
        int AfterIssueCount = 0);
}
