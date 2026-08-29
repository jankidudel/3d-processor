using Processor.Formats;

namespace Processor.ThreeMf;

public sealed class ThreeMfFormatException(string message)
    : ModelFormatException(
        message,
        message.Contains("does not support", StringComparison.Ordinal)
            ? "This 3MF file uses a feature that InspectMesh does not support yet."
            : "This 3MF file could not be analysed safely.");
