using Processor.Formats;

namespace Processor.Stl;

public abstract class StlFormatException(string message)
    : ModelFormatException(message, "This file is not a valid STL file or is incomplete.");

public sealed class UnrecognizedStlFormatException()
    : StlFormatException("The file format is not a recognized STL encoding.");

public sealed class MalformedBinaryStlException()
    : StlFormatException("Invalid binary STL. The file is incomplete or malformed.");

public sealed class MalformedAsciiStlException()
    : StlFormatException("Invalid ASCII STL. The file is incomplete or malformed.");
