using Processor.Stl;
using Processor.Validation;

namespace Processor;

public class ProcessorApplication
{
    public int Run(string[] args)
    {
        // var reader = new BinaryStlReader();
        // var validator = new StlValidator();

        // // we'll add processing here

        // return 0;

        if (args.Length == 0)
        {
            Console.WriteLine("Usage: <file.stl>");
            return 1;
        }

        var filePath = args[0];

        var reader = new BinaryStlReader();
        var mesh = reader.Read(filePath);

        Console.WriteLine($"Triangles: {mesh.Triangles.Count}");

        return 0;

    }
}