using Processor.Stl;
using Processor.Validation;

namespace Processor;

public class ProcessorApplication
{
    public int Run(string[] args)
    {
        var jsonOutput = args.Contains("--json", StringComparer.OrdinalIgnoreCase);
        var filePath = args.FirstOrDefault(arg => !arg.StartsWith("--", StringComparison.Ordinal));

        if (filePath is null)
        {
            Console.WriteLine("Usage: <file.stl> [--json]");
            return 1;
        }

        var reader = new BinaryStlReader();
        var mesh = reader.Read(filePath);

        var validator = new StlValidator();
        var validationResult = validator.Validate(mesh);

        var report = jsonOutput
            ? ValidationReportFormatter.FormatJson(validationResult)
            : ValidationReportFormatter.FormatText(validationResult);
        Console.WriteLine(report);

        return validationResult.IsValid ? 0 : 2;

    }
}
