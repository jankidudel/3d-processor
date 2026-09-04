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
                highlightMeshes: document.BuildItemMeshes);
        }
        var mesh = new StlReader().Read(filePath);
        var stlValidation = new MeshValidator().Validate(mesh);

        return new MeshQualityReportBuilder().Build(filePath, mesh, stlValidation, sourceFileName, "stl");
    }

    public StlRepairReport RepairStl(string filePath, string sourceFileName, string outputPath)
    {
        var mesh = new StlReader().Read(filePath);
        var validator = new MeshValidator();
        var reportBuilder = new MeshQualityReportBuilder();
        var before = reportBuilder.Build(filePath, mesh, validator.Validate(mesh), sourceFileName, "stl");
        var result = new StlSafeRepairer().Repair(mesh);
        if (result.CleanedMesh is null)
        {
            return new StlRepairReport
            {
                Before = before,
                DuplicateTrianglesRemoved = result.DuplicatesRemoved,
                DegenerateTrianglesRemoved = result.DegeneratesRemoved,
                OrientationsCorrected = result.OrientationsCorrected,
                SkippedReasons = result.SkippedReasons
            };
        }

        BinaryStlWriter.Write(outputPath, result.CleanedMesh.Triangles);
        var after = Process(outputPath, Path.GetFileName(outputPath));
        if (!DimensionsMatch(before, after))
        {
            File.Delete(outputPath);
            throw new InvalidOperationException("Safe repair changed the model dimensions.");
        }

        return new StlRepairReport
        {
            Before = before,
            After = after,
            DuplicateTrianglesRemoved = result.DuplicatesRemoved,
            DegenerateTrianglesRemoved = result.DegeneratesRemoved,
            OrientationsCorrected = result.OrientationsCorrected,
            SkippedReasons = result.SkippedReasons,
            HasCleanedFile = true,
            IsFullyValid = after.Validation.IsValid,
            CleanedFileName = Path.GetFileName(outputPath)
        };
    }

    public ThreeMfRepairReport RepairThreeMf(string filePath, string sourceFileName, string outputPath)
    {
        var before = Process(filePath, sourceFileName);
        var repair = new ThreeMfSafeRepairer().Repair(filePath, outputPath);
        var after = Process(outputPath, Path.GetFileName(outputPath));
        return new ThreeMfRepairReport
        {
            FileName = sourceFileName,
            FileSizeBytes = new FileInfo(filePath).Length,
            BeforeValidation = before,
            AfterValidation = after,
            Repair = new ThreeMfRepairSummary { RepairedFileAvailable = true, ModelsProcessed = repair.ModelsProcessed, TrianglesBefore = repair.TrianglesBefore, TrianglesAfter = repair.TrianglesAfter, DegenerateTrianglesRemoved = repair.DegenerateTrianglesRemoved, DuplicateTrianglesRemoved = repair.DuplicateTrianglesRemoved, InvalidIndexTrianglesRemoved = repair.InvalidIndexTrianglesRemoved, OrientationFixesApplied = repair.OrientationFixesApplied },
            Warnings = after.Validation.Issues.Where(issue => issue.Severity != "error").Select(issue => issue.Message).Distinct().ToArray()
        };
    }

    private static bool DimensionsMatch(MeshQualityReport before, MeshQualityReport after)
    {
        const double relativeTolerance = 1e-6;
        return Matches(before.Dimensions.Width, after.Dimensions.Width) &&
               Matches(before.Dimensions.Height, after.Dimensions.Height) &&
               Matches(before.Dimensions.Depth, after.Dimensions.Depth);

        static bool Matches(double left, double right)
        {
            return Math.Abs(left - right) <= relativeTolerance * Math.Max(1, Math.Max(Math.Abs(left), Math.Abs(right)));
        }
    }
}
