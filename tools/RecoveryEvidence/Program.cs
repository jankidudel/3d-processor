using Processor;
using Processor.Reporting;

var examples = Path.GetFullPath(args.Single());
var output = Path.Combine(examples, "quality-evidence");
Directory.CreateDirectory(output);
var processor = new ProcessorApplication();
var cases = new[]
{
    (Input: "collection-review/one-placed-cube.3mf", Output: "one-item-report.json", Count: 12, Width: 20f),
    (Input: "collection-review/two-placed-cubes.3mf", Output: "two-item-report.json", Count: 24, Width: 60f),
    (Input: "cad-export/duplicate-face-cube.3mf", Output: "duplicate-report.json", Count: 13, Width: 20f)
};
foreach (var item in cases)
{
    var report = processor.Process(Path.Combine(examples, item.Input));
    if (report.TriangleCount != item.Count || report.Dimensions.Width != item.Width)
    {
        throw new InvalidOperationException($"Control changed: {item.Input}");
    }
    File.WriteAllText(Path.Combine(output, item.Output), MeshQualityReportFormatter.FormatJson(report) + "\n");
}
var original = Path.Combine(examples, "cad-export/duplicate-face-cube.stl");
var cleanedPath = Path.Combine(output, "duplicate-cleaned.stl");
var repair = processor.RepairStl(original, Path.GetFileName(original), cleanedPath);
var reopened = processor.Process(cleanedPath);
if (reopened.TriangleCount != 12 || reopened.Dimensions.Width != 20 || !reopened.Validation.IsValid)
{
    throw new InvalidOperationException("Saved cleanup control did not retain the expected closed 20-unit cube.");
}
File.WriteAllText(Path.Combine(output, "cleaned-report.json"), MeshQualityReportFormatter.FormatJson(reopened) + "\n");
Console.WriteLine("Verified one-item, repeated-item, duplicate-face and saved-cleanup controls.");
