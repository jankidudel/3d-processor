using System.Text;
using Processor.Models;

namespace Processor.Stl;

public static class BinaryStlWriter
{
    public static void Write(string filePath, IReadOnlyList<Triangle> triangles)
    {
        using var stream = File.Create(filePath);
        using var writer = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: false);
        writer.Write(new byte[80]);
        writer.Write(checked((uint)triangles.Count));
        foreach (var triangle in triangles)
        {
            var normal = CalculateNormal(triangle.A, triangle.B, triangle.C);
            WriteVector(writer, normal);
            WriteVector(writer, triangle.A);
            WriteVector(writer, triangle.B);
            WriteVector(writer, triangle.C);
            writer.Write((ushort)0);
        }
    }

    private static Vector3 CalculateNormal(Vector3 a, Vector3 b, Vector3 c)
    {
        var abX = b.X - a.X;
        var abY = b.Y - a.Y;
        var abZ = b.Z - a.Z;
        var acX = c.X - a.X;
        var acY = c.Y - a.Y;
        var acZ = c.Z - a.Z;
        var x = abY * acZ - abZ * acY;
        var y = abZ * acX - abX * acZ;
        var z = abX * acY - abY * acX;
        var length = MathF.Sqrt(x * x + y * y + z * z);
        return length > 0 ? new Vector3(x / length, y / length, z / length) : new Vector3(0, 0, 0);
    }

    private static void WriteVector(BinaryWriter writer, Vector3 vector)
    {
        writer.Write(vector.X);
        writer.Write(vector.Y);
        writer.Write(vector.Z);
    }
}
