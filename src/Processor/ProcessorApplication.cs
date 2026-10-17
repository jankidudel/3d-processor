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

    public StlSplitReport SplitStl(string filePath, string sourceFileName, SplitAxis axis, double positionPercent, string partAPath, string partBPath)
    {
        var mesh = new StlReader().Read(filePath);
        var validator = new MeshValidator();
        var inputValidation = validator.Validate(mesh);
        if (!inputValidation.IsValid)
        {
            throw new SplitException("This STL must be closed, manifold and valid before it can be split. Check it with the STL Quality, Watertight or Manifold Checker, or use STL Repair where appropriate.");
        }
        var points = mesh.Triangles.SelectMany(triangle => new[] { triangle.A, triangle.B, triangle.C }).ToArray();
        var coordinates = points.Select(point => axis switch { SplitAxis.X => (double)point.X, SplitAxis.Y => point.Y, _ => point.Z }).ToArray();
        var minimum = coordinates.Min();
        var maximum = coordinates.Max();
        if (positionPercent <= 0 || positionPercent >= 100 || maximum <= minimum)
        {
            throw new SplitException("The cut position must be inside the model bounding box.");
        }
        var coordinate = minimum + (maximum - minimum) * positionPercent / 100d;
        var split = new StlSplitter().Split(mesh, axis, coordinate);
        BinaryStlWriter.Write(partAPath, split.PartA.Triangles);
        BinaryStlWriter.Write(partBPath, split.PartB.Triangles);
        var partA = Process(partAPath, Path.GetFileName(partAPath));
        var partB = Process(partBPath, Path.GetFileName(partBPath));
        if (!partA.Validation.IsValid || !partB.Validation.IsValid)
        {
            File.Delete(partAPath);
            File.Delete(partBPath);
            throw new SplitException("The cut could not be capped and validated safely; no output files were created.");
        }
        return new StlSplitReport
        {
            FileName = sourceFileName,
            FileSizeBytes = new FileInfo(filePath).Length,
            Split = new StlSplitSummary
            {
                Axis = axis.ToString(),
                PositionPercent = positionPercent,
                PositionCoordinate = coordinate,
                InputTriangleCount = mesh.Triangles.Count,
                PartA = ToPart(partA, Path.GetFileName(partAPath)),
                PartB = ToPart(partB, Path.GetFileName(partBPath))
            }
        };
    }

    private static StlSplitPart ToPart(MeshQualityReport report, string name) => new()
    {
        FileName = name,
        TriangleCount = report.TriangleCount,
        Dimensions = report.Dimensions,
        BoundingBox = report.BoundingBox,
        Validation = new StlSplitValidation
        {
            IsValid = report.Validation.IsValid,
            IsWatertight = report.Topology.OpenEdgeCount == 0,
            IsManifold = report.Topology.NonManifoldEdgeCount == 0,
            ConnectedComponentCount = report.Topology.ConnectedComponentCount
        }
    };

    public ThreeMfRepairReport RepairThreeMf(string filePath, string sourceFileName, string outputPath)
    {
        var before = Process(filePath, sourceFileName);
        var repair = new ThreeMfSafeRepairer().Repair(filePath, outputPath);
        var repairsApplied =
            repair.DegenerateTrianglesRemoved > 0 ||
            repair.DuplicateTrianglesRemoved > 0 ||
            repair.InvalidIndexTrianglesRemoved > 0 ||
            repair.OrientationFixesApplied > 0;
        if (!repairsApplied)
        {
            File.Delete(outputPath);
            return new ThreeMfRepairReport
            {
                FileName = sourceFileName,
                FileSizeBytes = new FileInfo(filePath).Length,
                BeforeValidation = before,
                AfterValidation = before,
                Repair = new ThreeMfRepairSummary
                {
                    RepairedFileAvailable = false,
                    ModelsProcessed = repair.ModelsProcessed,
                    TrianglesBefore = repair.TrianglesBefore,
                    TrianglesAfter = repair.TrianglesAfter,
                    DegenerateTrianglesRemoved = repair.DegenerateTrianglesRemoved,
                    DuplicateTrianglesRemoved = repair.DuplicateTrianglesRemoved,
                    InvalidIndexTrianglesRemoved = repair.InvalidIndexTrianglesRemoved,
                    OrientationFixesApplied = repair.OrientationFixesApplied
                },
                Warnings = before.Validation.Issues
                    .Where(issue => issue.Severity != "error")
                    .Select(issue => issue.Message)
                    .Distinct()
                    .ToArray()
            };
        }
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
