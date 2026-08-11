using Processor.Stl;
using Processor.Validation;

namespace Processor;

public class ProcessorApplication
{
    public int Run(string[] args)
    {
        if (args.Length == 0)
        {
            Console.WriteLine("Usage: <file.stl>");
            return 1;
        }

        var filePath = args[0];

        var reader = new BinaryStlReader();
        var mesh = reader.Read(filePath);

        var validator = new StlValidator();
        var validationResult = validator.Validate(mesh);

        Console.WriteLine($"Triangles: {mesh.Triangles.Count}");
        Console.WriteLine($"Validation: {(validationResult.IsValid ? "OK" : "FAIL")}");

        if (!validationResult.IsValid)
        {
            Console.WriteLine("Errors:");
            foreach (var err in validationResult.Errors)
            {
                Console.WriteLine($"- {err}");
            }
        }

        return validationResult.IsValid ? 0 : 2;

    }
}