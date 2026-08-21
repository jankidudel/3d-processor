using Processor.Models;

namespace Processor.Stl;

public class BinaryStlReader : IStlReader
{
    internal const int HeaderLength = 84;
    internal const int TriangleLength = 50;

    public Mesh Read(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        using var reader = new BinaryReader(stream);

        if (stream.Length < HeaderLength)
        {
            throw new MalformedBinaryStlException();
        }

        // Binary STL starts with an 80-byte header.
        reader.ReadBytes(80);

        uint triangleCount = reader.ReadUInt32();

        long expectedLength = HeaderLength + triangleCount * (long)TriangleLength;

        if (stream.Length < expectedLength)
        {
            throw new MalformedBinaryStlException();
        }

        var mesh = new Mesh();

        for (uint i = 0; i < triangleCount; i++)
        {
            var normal = ReadVector(reader);
            var a = ReadVector(reader);
            var b = ReadVector(reader);
            var c = ReadVector(reader);

            // Attribute byte count.
            // Normally zero; we don't need it yet.
            reader.ReadUInt16();

            mesh.Triangles.Add(
              new Triangle(normal, a, b, c)
            );
        }

        return mesh;
    }

    private static Vector3 ReadVector(BinaryReader reader)
    {
        return new Vector3(
          reader.ReadSingle(),
          reader.ReadSingle(),
          reader.ReadSingle()
        );
    }
}
