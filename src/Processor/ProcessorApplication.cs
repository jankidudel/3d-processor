using Processor.Stl;
using Processor.Validation;

namespace Processor;

public class ProcessorApplication
{
    public int Run(string[] args)
    {
        var reader = new BinaryStlReader();
        var validator = new StlValidator();

        // we'll add processing here

        return 0;
    }
}