using Processor.Models;

namespace Processor.Stl;

public class BinaryStlReader : IStlReader
{
    public Mesh Read(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        using var reader = new BinaryReader(stream);

        if (stream.Length < 84)
        {
            throw new InvalidDataException("File is too small to be a binary STL.");
        }

        // Binary STL starts with an 80-byte header.
        reader.ReadBytes(80);

        uint triangleCount = reader.ReadUInt32();

        long expectedLength = 84L + triangleCount * 50L;

        if (stream.Length < expectedLength)
        {
            throw new InvalidDataException(
              $"Invalid binary STL. Expected at least {expectedLength} bytes " +
              $"but file contains {stream.Length} bytes."
            );
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