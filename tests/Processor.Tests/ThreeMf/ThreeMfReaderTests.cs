using System.IO.Compression;
using Processor.Models;
using Processor.Reporting;
using Processor.ThreeMf;

namespace Processor.Tests.ThreeMf;

public sealed class ThreeMfReaderTests
{
    [Fact]
    public void Read_ComposesStandardComponentTranslationWithBuildRotation()
    {
        var path = CreatePackage("""
            <model xmlns="http://schemas.microsoft.com/3dmanufacturing/core/2015/02"><resources>
              <object id="1"><mesh><vertices><vertex x="1" y="2" z="3"/><vertex x="2" y="2" z="3"/><vertex x="1" y="3" z="3"/></vertices><triangles><triangle v1="0" v2="1" v3="2"/></triangles></mesh></object>
              <object id="2"><components><component objectid="1" transform="1 0 0 0 1 0 0 0 1 4 0 0"/></components></object>
            </resources><build><item objectid="2" transform="0 1 0 -1 0 0 0 0 1 10 20 30"/></build></model>
            """);
        try
        {
            var triangle = Assert.Single(new ThreeMfReader().Read(path).Mesh.Triangles);
            // (1,2,3) -> component (5,2,3) -> 90-degree Z rotation
            // (-2,5,3) -> build translation (8,25,33).
            Assert.Equal(new Vector3(8, 25, 33), triangle.A);
            Assert.Equal(new Vector3(8, 26, 33), triangle.B);
            Assert.Equal(new Vector3(7, 25, 33), triangle.C);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Read_ResolvesRootRelationshipAndBuildTransform()
    {
        var path = CreatePackage("""
            <model unit="inch" xmlns="http://schemas.microsoft.com/3dmanufacturing/core/2015/02">
              <resources><object id="1" type="model"><mesh><vertices>
                <vertex x="0" y="0" z="0"/><vertex x="1" y="0" z="0"/><vertex x="0" y="1" z="0"/>
              </vertices><triangles><triangle v1="0" v2="1" v3="2"/></triangles></mesh></object></resources>
              <build><item objectid="1" transform="1 0 0 0 1 0 0 0 1 10 0 0"/></build>
            </model>
            """);
        try
        {
            var document = new ThreeMfReader().Read(path);
            Assert.Equal("inch", document.SourceUnit);
            Assert.Equal(1, document.BuildItemCount);
            Assert.Single(document.Mesh.Triangles);
            Assert.Equal(10, document.Mesh.Triangles[0].A.X);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Read_RejectsTriangleOutsideVertexRange()
    {
        var path = CreatePackage("""
            <model xmlns="http://schemas.microsoft.com/3dmanufacturing/core/2015/02"><resources><object id="1"><mesh><vertices><vertex x="0" y="0" z="0"/></vertices><triangles><triangle v1="0" v2="1" v3="0"/></triangles></mesh></object></resources><build><item objectid="1"/></build></model>
            """);
        try
        {
            Assert.Throws<ThreeMfFormatException>(() => new ThreeMfReader().Read(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Read_RejectsRequiredExtensions()
    {
        var path = CreatePackage("""
            <model requiredextensions="p" xmlns="http://schemas.microsoft.com/3dmanufacturing/core/2015/02"><resources/><build/></model>
            """);
        try
        {
            var exception = Assert.Throws<ThreeMfFormatException>(() => new ThreeMfReader().Read(path));
            Assert.Contains("does not support", exception.Message);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Read_RejectsDtdInModelXml()
    {
        var path = CreatePackage("""
            <!DOCTYPE model [<!ENTITY unsafe "value">]><model xmlns="http://schemas.microsoft.com/3dmanufacturing/core/2015/02"><resources/><build/></model>
            """);
        try
        {
            Assert.Throws<ThreeMfFormatException>(() => new ThreeMfReader().Read(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Read_RejectsPackageWithoutRootRelationships()
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.3mf");
        using (ZipFile.Open(path, ZipArchiveMode.Create))
        {
        }
        try
        {
            Assert.Throws<ThreeMfFormatException>(() => new ThreeMfReader().Read(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Read_RejectsCyclicComponents()
    {
        var path = CreatePackage("""
            <model xmlns="http://schemas.microsoft.com/3dmanufacturing/core/2015/02"><resources>
              <object id="1"><components><component objectid="2"/></components></object>
              <object id="2"><components><component objectid="1"/></components></object>
            </resources><build><item objectid="1"/></build></model>
            """);
        try
        {
            Assert.Throws<ThreeMfFormatException>(() => new ThreeMfReader().Read(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Read_RejectsSingularBuildTransform()
    {
        var path = CreatePackage("""
            <model xmlns="http://schemas.microsoft.com/3dmanufacturing/core/2015/02"><resources><object id="1"><mesh><vertices><vertex x="0" y="0" z="0"/></vertices><triangles/></mesh></object></resources><build><item objectid="1" transform="0 0 0 0 0 0 0 0 0 0 0 0"/></build></model>
            """);
        try
        {
            Assert.Throws<ThreeMfFormatException>(() => new ThreeMfReader().Read(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Read_RejectsBuildWithoutUsableGeometry()
    {
        var path = CreatePackage("""
            <model xmlns="http://schemas.microsoft.com/3dmanufacturing/core/2015/02"><resources><object id="1"><mesh><vertices><vertex x="0" y="0" z="0"/></vertices><triangles/></mesh></object></resources><build><item objectid="1"/></build></model>
            """);
        try
        {
            var exception = Assert.Throws<ThreeMfFormatException>(() => new ThreeMfReader().Read(path));
            Assert.Contains("usable build geometry", exception.Message);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Read_RejectsUnsafeCompressionRatio()
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.3mf");
        using (var archive = ZipFile.Open(path, ZipArchiveMode.Create))
        {
            Write(archive, "_rels/.rels", """<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rel" Type="http://schemas.microsoft.com/3dmanufacturing/2013/01/3dmodel" Target="3D/model.model"/></Relationships>""");
            Write(archive, "3D/model.model", new string('a', 100_000));
        }
        try
        {
            Assert.Throws<ThreeMfFormatException>(() => new ThreeMfReader().Read(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Read_ResolvesComponentGeometry()
    {
        var path = CreatePackage("""
            <model xmlns="http://schemas.microsoft.com/3dmanufacturing/core/2015/02"><resources>
              <object id="1"><mesh><vertices><vertex x="0" y="0" z="0"/><vertex x="1" y="0" z="0"/><vertex x="0" y="1" z="0"/></vertices><triangles><triangle v1="0" v2="1" v3="2"/></triangles></mesh></object>
              <object id="2"><components><component objectid="1" transform="1 0 0 0 1 0 0 0 1 2 0 0"/></components></object>
            </resources><build><item objectid="2" transform="1 0 0 0 1 0 0 0 1 3 0 0"/></build></model>
            """);
        try
        {
            var document = new ThreeMfReader().Read(path);
            Assert.Single(document.Mesh.Triangles);
            Assert.Equal(5, document.Mesh.Triangles[0].A.X);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Read_RejectsPackageWithTooManyEntries()
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.3mf");
        using (var archive = ZipFile.Open(path, ZipArchiveMode.Create))
        {
            for (var index = 0; index <= 1_000; index++)
            {
                archive.CreateEntry($"extra/{index}");
            }
        }
        try
        {
            Assert.Throws<ThreeMfFormatException>(() => new ThreeMfReader().Read(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Read_RejectsComponentGraphDeeperThanLimit()
    {
        var objects = string.Join("", Enumerable.Range(1, 34).Select(index =>
            $"<object id=\"{index}\"><components><component objectid=\"{index + 1}\"/></components></object>"));
        var path = CreatePackage($"""
            <model xmlns="http://schemas.microsoft.com/3dmanufacturing/core/2015/02"><resources>{objects}<object id="35"><mesh><vertices><vertex x="0" y="0" z="0"/></vertices><triangles/></mesh></object></resources><build><item objectid="1"/></build></model>
            """);
        try
        {
            Assert.Throws<ThreeMfFormatException>(() => new ThreeMfReader().Read(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Read_RejectsNonZipFile()
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.3mf");
        File.WriteAllText(path, "this is not a zip archive");
        try
        {
            Assert.Throws<ThreeMfFormatException>(() => new ThreeMfReader().Read(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void ValidateBuildItems_DoesNotTreatSeparateInstancesAsDuplicates()
    {
        var triangle = new Triangle(new Vector3(0, 0, 0), new Vector3(0, 0, 0), new Vector3(1, 0, 0), new Vector3(0, 1, 0));
        var first = new Mesh();
        first.Triangles.Add(triangle);
        var second = new Mesh();
        second.Triangles.Add(triangle);

        var validation = ThreeMfValidationAggregator.ValidateBuildItems([first, second]);

        Assert.Equal(0, validation.CountIssues("duplicate_triangle"));
    }

    [Fact]
    public void BuildReport_SamplesOpenEdgesForEveryResolvedRotatedBuildItem()
    {
        var path = CreatePackage("""
            <model xmlns="http://schemas.microsoft.com/3dmanufacturing/core/2015/02"><resources>
              <object id="1"><mesh><vertices><vertex x="0" y="0" z="0"/><vertex x="1" y="0" z="0"/><vertex x="0" y="1" z="0"/></vertices><triangles><triangle v1="0" v2="1" v3="2"/></triangles></mesh></object>
            </resources><build>
              <item objectid="1" transform="1 0 0 0 1 0 0 0 1 10 0 0"/>
              <item objectid="1" transform="0 1 0 -1 0 0 0 0 1 0 20 0"/>
            </build></model>
            """);
        try
        {
            var document = new ThreeMfReader().Read(path);
            var report = new MeshQualityReportBuilder().Build(
                path,
                document.Mesh,
                ThreeMfValidationAggregator.ValidateBuildItems(document.BuildItemMeshes),
                fileFormat: "3mf",
                highlightMeshes: document.BuildItemMeshes);

            var edges = Assert.IsType<MeshIssueGeometry>(report.IssueGeometry).OpenEdges!;
            Assert.Equal(6, edges.TotalCount);
            Assert.Equal(6, edges.ReturnedCount);
            Assert.False(edges.Truncated);
            Assert.Contains(edges.Segments, edge => edge.Start.X >= 10 || edge.End.X >= 10);
            Assert.Contains(edges.Segments, edge => edge.Start.Y >= 20 || edge.End.Y >= 20);
            var json = MeshQualityReportFormatter.FormatJson(report);
            Assert.Contains("\"issueGeometry\"", json);
            Assert.Contains("\"totalCount\": 6", json);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Read_RejectsOversizedPackageEntry()
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.3mf");
        using (var archive = ZipFile.Open(path, ZipArchiveMode.Create))
        {
            var entry = archive.CreateEntry("large.bin", CompressionLevel.NoCompression);
            using var stream = entry.Open();
            var buffer = new byte[1024 * 1024];
            for (var index = 0; index <= 64; index++)
            {
                stream.Write(buffer);
            }
        }
        try
        {
            Assert.Throws<ThreeMfFormatException>(() => new ThreeMfReader().Read(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Read_RejectsPackageWithTooMuchExpandedData()
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.3mf");
        using (var archive = ZipFile.Open(path, ZipArchiveMode.Create))
        {
            var buffer = new byte[1024 * 1024];
            for (var entryIndex = 0; entryIndex < 3; entryIndex++)
            {
                var entry = archive.CreateEntry($"part-{entryIndex}.bin", CompressionLevel.NoCompression);
                using var stream = entry.Open();
                for (var megabyte = 0; megabyte < 43; megabyte++)
                {
                    stream.Write(buffer);
                }
            }
        }
        try
        {
            Assert.Throws<ThreeMfFormatException>(() => new ThreeMfReader().Read(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static string CreatePackage(string model)
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.3mf");
        using var archive = ZipFile.Open(path, ZipArchiveMode.Create);
        Write(archive, "_rels/.rels", """<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rel" Type="http://schemas.microsoft.com/3dmanufacturing/2013/01/3dmodel" Target="3D/custom.model"/></Relationships>""");
        Write(archive, "3D/custom.model", model);
        return path;
    }

    private static void Write(ZipArchive archive, string name, string content)
    {
        using var writer = new StreamWriter(archive.CreateEntry(name).Open());
        writer.Write(content);
    }
}
