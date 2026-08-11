using Processor.Reporting;
using Processor.Stl;
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
                ? StlQualityReportFormatter.FormatJson(report)
                : StlQualityReportFormatter.FormatText(report);
            outputWriter.WriteLine(output);

            return report.Validation.IsValid ? 0 : 2;
        }
        catch (Exception exception)
        {
            errorWriter.WriteLine($"Processing failed: {exception.Message}");
            return 1;
        }
    }

    public StlQualityReport Process(string filePath)
    {
        var mesh = new BinaryStlReader().Read(filePath);
        var validation = new StlValidator().Validate(mesh);

        return new StlQualityReportBuilder().Build(filePath, mesh, validation);
    }
}
