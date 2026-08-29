using Processor.Reporting;
using Processor.Stl;
using Processor.ThreeMf;
using Processor.Validation;

namespace Processor;

public class ProcessorApplication
{
    public int Run(string[] args)
    {
        return Run(args, Console.Out, Console.Error);
    }

    public int Run(string[] args, TextWriter outputWriter, TextWriter errorWriter)
    {
        var jsonOutput = args.Contains("--json", StringComparer.OrdinalIgnoreCase);
        var filePath = args.FirstOrDefault(arg => !arg.StartsWith("--", StringComparison.Ordinal));

        if (filePath is null)
        {
            errorWriter.WriteLine("Usage: <file.stl> [--json]");
            return 1;
        }

        try
        {
            var report = Process(filePath);
            var output = jsonOutput
                ? MeshQualityReportFormatter.FormatJson(report)
                : MeshQualityReportFormatter.FormatText(report);
            outputWriter.WriteLine(output);

            return report.Validation.IsValid ? 0 : 2;
        }
        catch (Exception exception)
        {
            errorWriter.WriteLine($"Processing failed: {exception.Message}");
            return 1;
        }
    }

    public MeshQualityReport Process(string filePath, string? sourceFileName = null)
    {
        if (string.Equals(Path.GetExtension(filePath), ".3mf", StringComparison.OrdinalIgnoreCase))
        {
            var document = new ThreeMfReader().Read(filePath);
            var validation = ThreeMfValidationAggregator.ValidateBuildItems(document.BuildItemMeshes);
            return new MeshQualityReportBuilder().Build(
                filePath,
                document.Mesh,
                validation,
                sourceFileName,
                "3mf",
                document.SourceUnit,
                document.ModelObjectCount,
                document.BuildItemCount,
                includeIssueHighlights: false);
        }
        var mesh = new StlReader().Read(filePath);
        var stlValidation = new MeshValidator().Validate(mesh);

        return new MeshQualityReportBuilder().Build(filePath, mesh, stlValidation, sourceFileName, "stl");
    }
}
