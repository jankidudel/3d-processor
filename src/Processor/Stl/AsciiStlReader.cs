using System.Globalization;
using Processor.Models;

namespace Processor.Stl;

public class AsciiStlReader : IStlReader
{
    public Mesh Read(string filePath)
    {
        using var reader = new StreamReader(filePath, detectEncodingFromByteOrderMarks: true);
        var mesh = new Mesh();
        var lines = new SignificantLineReader(reader);

        RequireKeyword(lines.Read(), "solid", allowSuffix: true);

        while (true)
        {
            var line = lines.Read() ?? throw new MalformedAsciiStlException();
            if (HasKeyword(line, "endsolid"))
            {
                RequireKeyword(line, "endsolid", allowSuffix: true);
                if (lines.Read() is not null)
                {
                    throw new MalformedAsciiStlException();
                }

                return mesh;
            }

            var normal = ParseVector(line, "facet", "normal");
            RequireKeyword(lines.Read(), "outer", "loop");
            var a = ParseVector(lines.Read(), "vertex");
            var b = ParseVector(lines.Read(), "vertex");
            var c = ParseVector(lines.Read(), "vertex");
            RequireKeyword(lines.Read(), "endloop");
            RequireKeyword(lines.Read(), "endfacet");
            mesh.Triangles.Add(new Triangle(normal, a, b, c));
        }
    }

    private static Vector3 ParseVector(string? line, params string[] keywords)
    {
        var tokens = Split(line);
        if (tokens.Length != keywords.Length + 3 || !KeywordsMatch(tokens, keywords))
        {
            throw new MalformedAsciiStlException();
        }

        return new Vector3(
            ParseNumber(tokens[^3]),
            ParseNumber(tokens[^2]),
            ParseNumber(tokens[^1]));
    }

    private static float ParseNumber(string token)
    {
        if (!float.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
        {
            throw new MalformedAsciiStlException();
        }

        return value;
    }

    private static void RequireKeyword(string? line, params string[] keywords)
    {
        RequireKeyword(line, false, keywords);
    }

    private static void RequireKeyword(string? line, bool allowSuffix, params string[] keywords)
    {
        var tokens = Split(line);
        if ((!allowSuffix && tokens.Length != keywords.Length) ||
            (allowSuffix && tokens.Length < keywords.Length) ||
            !KeywordsMatch(tokens, keywords))
        {
            throw new MalformedAsciiStlException();
        }
    }

    private static void RequireKeyword(string? line, string keyword, bool allowSuffix)
    {
        RequireKeyword(line, allowSuffix, keyword);
    }

    private static bool HasKeyword(string line, string keyword)
    {
        var tokens = Split(line);
        return tokens.Length > 0 && tokens[0].Equals(keyword, StringComparison.OrdinalIgnoreCase);
    }

    private static bool KeywordsMatch(string[] tokens, string[] keywords)
    {
        for (var i = 0; i < keywords.Length; i++)
        {
            if (!tokens[i].Equals(keywords[i], StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    private static string[] Split(string? line)
    {
        return line?.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries) ?? [];
    }

    private sealed class SignificantLineReader(TextReader reader)
    {
        public string? Read()
        {
            while (reader.ReadLine() is { } line)
            {
                if (!string.IsNullOrWhiteSpace(line))
                {
                    return line;
                }
            }

            return null;
        }
    }
}
