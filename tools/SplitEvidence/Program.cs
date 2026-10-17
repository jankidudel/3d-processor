using System.Security.Cryptography;
using System.Text.Json;
using Processor;
using Processor.Stl;

if (args.Length != 1 || !Directory.Exists(args[0]))
{
    Console.Error.WriteLine("Usage: SplitEvidence <frontend public/examples directory>");
    return 1;
}

var examples = Path.GetFullPath(args[0]);
var output = Path.Combine(examples, "split-assembly");
Directory.CreateDirectory(output);
var processor = new ProcessorApplication();
var runs = new List<object>();
foreach (var (name, relativePath, expectedSuccess) in new[]
{
    ("cube", "english-market-batch/closed-20mm-cube.stl", true),
    ("shifted-cube", "slicer-diagnosis/shifted-cube.stl", true),
    ("open-cube", "english-market-batch/open-face-cube.stl", false),
    ("hollow-channel", "split-assembly/hollow-channel-20x20x20.stl", false)
})
{
    var input = Path.Combine(examples, relativePath);
    var before = processor.Process(input);
    var lowerPath = Path.Combine(output, $"{name}-lower-z.stl");
    var upperPath = Path.Combine(output, $"{name}-upper-z.stl");
    try
    {
        var split = processor.SplitStl(input, Path.GetFileName(input), SplitAxis.Z, 50, lowerPath, upperPath);
        if (!expectedSuccess)
        {
            throw new InvalidOperationException($"Unexpected acceptance: {name}");
        }
        var lower = processor.Process(lowerPath);
        var upper = processor.Process(upperPath);
        if (!lower.Validation.IsValid || !upper.Validation.IsValid ||
            lower.Dimensions.Depth != 10 || upper.Dimensions.Depth != 10 ||
            Math.Abs((lower.Volume ?? 0) + (upper.Volume ?? 0) - 8000) > 0.001)
        {
            throw new InvalidOperationException($"Output reconstruction checks failed: {name}");
        }
        runs.Add(new
        {
            name,
            input = relativePath,
            inputSha256 = Hash(input),
            axis = "Z",
            positionPercent = 50,
            plane = split.Split.PositionCoordinate,
            source = Summary(before),
            outcome = "accepted",
            outputs = new[]
            {
                new { path = Path.GetFileName(lowerPath), sha256 = Hash(lowerPath), observation = Summary(lower) },
                new { path = Path.GetFileName(upperPath), sha256 = Hash(upperPath), observation = Summary(upper) }
            }
        });
    }
    catch (SplitException exception) when (!expectedSuccess)
    {
        runs.Add(new
        {
            name,
            input = relativePath,
            inputSha256 = Hash(input),
            axis = "Z",
            positionPercent = 50,
            plane = before.BoundingBox.MinZ + before.Dimensions.Depth / 2,
            source = Summary(before),
            outcome = "rejected",
            localReason = exception.Message,
            publicServiceCode = "split_could_not_be_capped_safely"
        });
    }
}

var coupons = new[] { "alignment-block-a.stl", "alignment-block-b.stl", "alignment-dowel.stl" }
    .Select(name =>
    {
        var path = Path.Combine(output, name);
        var report = processor.Process(path);
        if (!report.Validation.IsValid)
        {
            throw new InvalidOperationException($"Invalid coupon: {name}");
        }
        return new
        {
            path = name,
            sha256 = Hash(path),
            observation = Summary(report)
        };
    }).ToArray();
var evidence = new
{
    schemaVersion = 1,
    observedDate = "2026-10-17",
    method = "Local ProcessorApplication.SplitStl; serialized binary outputs reopened with ProcessorApplication.Process",
    revision = new
    {
        baselineCommit = "1edf982",
        splitterSha256 = Hash(Path.Combine(AppContext.BaseDirectory, "../../../../../src/Processor/Stl/StlSplitter.cs")),
        applicationSha256 = Hash(Path.Combine(AppContext.BaseDirectory, "../../../../../src/Processor/ProcessorApplication.cs")),
        runtime = Environment.Version.ToString()
    },
    reproduction = "From 3d-processor: dotnet run --project tools/SplitEvidence -- ../3d-frontend/public/examples",
    unitAssumption = "millimetres; STL does not declare units",
    physicalTest = false,
    productionJob = false,
    runs,
    coupons
};
var jsonOptions = new JsonSerializerOptions
{
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    WriteIndented = true
};
File.WriteAllText(Path.Combine(output, "observations.json"), JsonSerializer.Serialize(evidence, jsonOptions) + "\n");
Console.WriteLine($"Verified {runs.Count} split controls and {coupons.Length} coupon meshes; observations.json written.");
return 0;

static string Hash(string path)
{
    return Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path)));
}

static object Summary(Processor.Reporting.MeshQualityReport report)
{
    return new
    {
        report.TriangleCount,
        report.BoundingBox,
        report.Dimensions,
        report.Volume,
        report.Topology,
        valid = report.Validation.IsValid,
        issues = report.Validation.Issues.Select(issue => issue.Code).Distinct().ToArray()
    };
}
