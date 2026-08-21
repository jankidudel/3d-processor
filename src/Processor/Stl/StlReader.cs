using Processor.Models;

namespace Processor.Stl;

public sealed class StlReader : IStlReader
{
    public Mesh Read(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        var format = DetectFormat(stream);

        return format switch
        {
            StlFormat.Binary => new BinaryStlReader().Read(filePath),
            StlFormat.Ascii => new AsciiStlReader().Read(filePath),
            _ => throw new UnrecognizedStlFormatException()
        };
    }

    private static StlFormat DetectFormat(FileStream stream)
    {
        if (stream.Length >= BinaryStlReader.HeaderLength)
        {
            Span<byte> header = stackalloc byte[BinaryStlReader.HeaderLength];
            stream.ReadExactly(header);
            var triangleCount = BitConverter.ToUInt32(header[80..]);
            var expectedLength = BinaryStlReader.HeaderLength + (long)triangleCount * BinaryStlReader.TriangleLength;

            // A text-like "solid" prefix is checked before the binary length,
            // because ASCII bytes at offsets 80-83 can coincidentally look like
            // a plausible triangle count. Real binary count/triangle data almost
            // invariably contains control bytes, including when its header starts
            // with "solid".
            if (!ContainsBinaryControlBytes(header) && StartsWithAsciiSolid(header))
            {
                return StlFormat.Ascii;
            }

            if (expectedLength <= stream.Length)
            {
                return StlFormat.Binary;
            }

            if (ContainsBinaryControlBytes(header))
            {
                throw new MalformedBinaryStlException();
            }
        }

        stream.Position = 0;
        using var reader = new StreamReader(stream, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        while (reader.ReadLine() is { } line)
        {
            var trimmed = line.TrimStart();
            if (trimmed.Length == 0)
            {
                continue;
            }

            return StartsWithKeyword(trimmed, "solid")
                ? StlFormat.Ascii
                : stream.Length >= BinaryStlReader.HeaderLength
                    ? throw new MalformedBinaryStlException()
                    : throw new UnrecognizedStlFormatException();
        }

        throw new UnrecognizedStlFormatException();
    }

    private static bool ContainsBinaryControlBytes(ReadOnlySpan<byte> bytes)
    {
        foreach (var value in bytes)
        {
            if (value is < 0x09 or (>= 0x0e and < 0x20))
            {
                return true;
            }
        }

        return false;
    }

    private static bool StartsWithAsciiSolid(ReadOnlySpan<byte> bytes)
    {
        var index = 0;
        while (index < bytes.Length && char.IsWhiteSpace((char)bytes[index]))
        {
            index++;
        }

        ReadOnlySpan<byte> keyword = "solid"u8;
        if (index + keyword.Length > bytes.Length)
        {
            return false;
        }

        for (var keywordIndex = 0; keywordIndex < keyword.Length; keywordIndex++)
        {
            if (char.ToLowerInvariant((char)bytes[index + keywordIndex]) != (char)keyword[keywordIndex])
            {
                return false;
            }
        }

        return index + keyword.Length == bytes.Length ||
            char.IsWhiteSpace((char)bytes[index + keyword.Length]);
    }

    private static bool StartsWithKeyword(string text, string keyword)
    {
        return text.StartsWith(keyword, StringComparison.OrdinalIgnoreCase) &&
            (text.Length == keyword.Length || char.IsWhiteSpace(text[keyword.Length]));
    }

    private enum StlFormat
    {
        Binary,
        Ascii
    }
}
