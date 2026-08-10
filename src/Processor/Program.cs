using Processor.Stl;
using Processor.Validation;

namespace Processor;

public class Program
{
    public static int Main(string[] args)
    {
        if (args.Length == 0)
        {
            Console.WriteLine("Usage: dotnet run -- <file.stl>");
            return 1;
        }

        return ProcessFile(args[0]);
    }

    private static int ProcessFile(string filePath)
    {
        if (!File.Exists(filePath))
        {
            Console.WriteLine($"File does not exist: {filePath}");
            return 1;
        }

        try
        {
            var reader = new BinaryStlReader();
            var validator = new StlValidator();

            var mesh = reader.Read(filePath);
            var result = validator.Validate(mesh);

            Console.WriteLine($"Triangles: {mesh.Triangles.Count}");
            Console.WriteLine($"Validation: {(result.IsValid ? "PASS" : "FAIL")}");

            foreach (var error in result.Errors)
            {
                Console.WriteLine($"- {error}");
            }

            return result.IsValid ? 0 : 2;
        }
        catch (Exception exception)
        {
            Console.WriteLine($"Failed to process STL: {exception.Message}");
            return 1;
        }
    }
}