using System.IO.Compression;
using System.Xml;
using System.Xml.Linq;
using Processor.Models;

namespace Processor.ThreeMf;

public sealed class ThreeMfReader
{
    private const int MaxEntryCount = 1_000;
    private const long MaxEntrySizeBytes = 64L * 1024 * 1024;
    private const long MaxUncompressedSizeBytes = 128L * 1024 * 1024;
    private const double MaxCompressionRatio = 100;
    private const int MaxComponentDepth = 32;

    public ThreeMfDocument Read(string filePath)
    {
        try
        {
            using var archive = ZipFile.OpenRead(filePath);
            ValidateArchive(archive);
            var modelPart = ResolveModelPart(archive);
            var model = ReadModel(archive, modelPart);
            return ResolveGeometry(model);
        }
        catch (InvalidDataException)
        {
            throw new ThreeMfFormatException("This file is not a valid 3MF package.");
        }
        catch (XmlException)
        {
            throw new ThreeMfFormatException("This 3MF file contains invalid model data.");
        }
    }

    private static void ValidateArchive(ZipArchive archive)
    {
        if (archive.Entries.Count > MaxEntryCount)
        {
            throw new ThreeMfFormatException("This 3MF file contains too many package entries.");
        }

        long totalLength = 0;
        foreach (var entry in archive.Entries)
        {
            if (entry.Length > MaxEntrySizeBytes)
            {
                throw new ThreeMfFormatException("This 3MF file contains an entry that is too large.");
            }
            totalLength += entry.Length;
            if (totalLength > MaxUncompressedSizeBytes)
            {
                throw new ThreeMfFormatException("This 3MF file expands to too much data.");
            }
            if (entry.Length > 0 && entry.CompressedLength > 0 &&
                (double)entry.Length / entry.CompressedLength > MaxCompressionRatio)
            {
                throw new ThreeMfFormatException("This 3MF file has an unsafe compression ratio.");
            }
        }
    }

    private static string ResolveModelPart(ZipArchive archive)
    {
        var relationships = ReadXml(RequiredEntry(archive, "_rels/.rels"));
        var relationship = relationships.Descendants()
            .FirstOrDefault(element => element.Name.LocalName == "Relationship" &&
                (string?)element.Attribute("Type") is { } type &&
                type.EndsWith("/3dmodel", StringComparison.OrdinalIgnoreCase));
        var target = (string?)relationship?.Attribute("Target");
        if (string.IsNullOrWhiteSpace(target) || target.Contains("..", StringComparison.Ordinal) ||
            Uri.IsWellFormedUriString(target, UriKind.Absolute))
        {
            throw new ThreeMfFormatException("This 3MF file does not contain a supported model part.");
        }
        return target.TrimStart('/').Replace('\\', '/');
    }

    private static ParsedModel ReadModel(ZipArchive archive, string modelPart)
    {
        var document = ReadXml(RequiredEntry(archive, modelPart));
        var root = document.Root ?? throw new ThreeMfFormatException("This 3MF model is empty.");
        if (!string.IsNullOrWhiteSpace((string?)root.Attribute("requiredextensions")) ||
            root.Descendants().Any(element => element.Name.LocalName == "requiredextensions"))
        {
            throw new ThreeMfFormatException("This 3MF file uses a feature that InspectMesh does not support yet.");
        }
        var unit = (string?)root.Attribute("unit") ?? "millimeter";
        var objects = root.Descendants().Where(element => element.Name.LocalName == "object")
            .Select(ParseObject).ToDictionary(value => value.Id);
        var items = root.Descendants().Where(element => element.Name.LocalName == "build")
            .SelectMany(build => build.Elements().Where(element => element.Name.LocalName == "item"))
            .Select(ParseReference).ToList();
        if (items.Count == 0)
        {
            throw new ThreeMfFormatException("This 3MF file does not contain build items.");
        }
        return new ParsedModel(unit, objects, items);
    }

    private static ParsedObject ParseObject(XElement element)
    {
        var id = ParseInt((string?)element.Attribute("id"));
        var mesh = element.Elements().FirstOrDefault(value => value.Name.LocalName == "mesh");
        if (mesh is not null)
        {
            var vertices = mesh.Descendants().Where(value => value.Name.LocalName == "vertex")
                .Select(value => new Vector3(ParseFloat((string?)value.Attribute("x")), ParseFloat((string?)value.Attribute("y")), ParseFloat((string?)value.Attribute("z")))).ToList();
            var triangles = mesh.Descendants().Where(value => value.Name.LocalName == "triangle")
                .Select(value => (ParseInt((string?)value.Attribute("v1")), ParseInt((string?)value.Attribute("v2")), ParseInt((string?)value.Attribute("v3")))).ToList();
            if (triangles.Any(value => value.Item1 < 0 || value.Item2 < 0 || value.Item3 < 0 || value.Item1 >= vertices.Count || value.Item2 >= vertices.Count || value.Item3 >= vertices.Count))
            {
                throw new ThreeMfFormatException("This 3MF file contains an invalid triangle reference.");
            }
            return new ParsedObject(id, vertices, triangles, []);
        }
        var components = element.Descendants().Where(value => value.Name.LocalName == "component").Select(ParseReference).ToList();
        if (components.Count == 0)
        {
            throw new ThreeMfFormatException("This 3MF file contains an unsupported object.");
        }
        return new ParsedObject(id, [], [], components);
    }

    private static ThreeMfDocument ResolveGeometry(ParsedModel model)
    {
        var mesh = new Mesh();
        var buildItemMeshes = new List<Mesh>();
        foreach (var item in model.BuildItems)
        {
            var buildItemMesh = new Mesh();
            ResolveObject(item.ObjectId, item.Transform, model.Objects, buildItemMesh, [], 0);
            buildItemMeshes.Add(buildItemMesh);
            mesh.Triangles.AddRange(buildItemMesh.Triangles);
        }
        if (mesh.Triangles.Count == 0)
        {
            throw new ThreeMfFormatException("This 3MF file does not contain usable build geometry.");
        }
        return new ThreeMfDocument(mesh, buildItemMeshes, model.Unit, model.Objects.Count, model.BuildItems.Count);
    }

    private static void ResolveObject(int id, Transform transform, Dictionary<int, ParsedObject> objects, Mesh mesh, HashSet<int> stack, int depth)
    {
        if (depth > MaxComponentDepth || !stack.Add(id) || !objects.TryGetValue(id, out var item))
        {
            throw new ThreeMfFormatException("This 3MF file contains unsupported component references.");
        }
        foreach (var triangle in item.Triangles)
        {
            mesh.Triangles.Add(new Triangle(new Vector3(0, 0, 0), transform.Apply(item.Vertices[triangle.A]), transform.Apply(item.Vertices[triangle.B]), transform.Apply(item.Vertices[triangle.C])));
        }
        foreach (var component in item.Components)
        {
            ResolveObject(component.ObjectId, transform.Compose(component.Transform), objects, mesh, stack, depth + 1);
        }
        stack.Remove(id);
    }

    private static Reference ParseReference(XElement element) => new(ParseInt((string?)element.Attribute("objectid")), Transform.Parse((string?)element.Attribute("transform")));
    private static int ParseInt(string? value) => int.TryParse(value, out var result) ? result : throw new ThreeMfFormatException("This 3MF file contains invalid model data.");
    private static float ParseFloat(string? value) => float.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var result) && float.IsFinite(result) ? result : throw new ThreeMfFormatException("This 3MF file contains invalid model data.");
    private static ZipArchiveEntry RequiredEntry(ZipArchive archive, string name) => archive.GetEntry(name) ?? throw new ThreeMfFormatException("This 3MF file does not contain a supported model part.");
    private static XDocument ReadXml(ZipArchiveEntry entry)
    {
        using var stream = entry.Open();
        using var reader = XmlReader.Create(stream, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 16 * 1024 * 1024 });
        return XDocument.Load(reader, LoadOptions.None);
    }

    private sealed record ParsedModel(string Unit, Dictionary<int, ParsedObject> Objects, List<Reference> BuildItems);
    private sealed record ParsedObject(int Id, List<Vector3> Vertices, List<(int A, int B, int C)> Triangles, List<Reference> Components);
    private sealed record Reference(int ObjectId, Transform Transform);
    private sealed record Transform(float M00, float M01, float M02, float M03, float M10, float M11, float M12, float M13, float M20, float M21, float M22, float M23)
    {
        public static Transform Identity { get; } = new(1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0);
        public static Transform Parse(string? text)
        {
            if (string.IsNullOrWhiteSpace(text)) { return Identity; }
            var values = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Select(ParseFloat).ToArray();
            if (values.Length != 12) { throw new ThreeMfFormatException("This 3MF file contains an invalid transform."); }
            var result = new Transform(values[0], values[1], values[2], values[3], values[4], values[5], values[6], values[7], values[8], values[9], values[10], values[11]);
            var determinant = result.M00 * (result.M11 * result.M22 - result.M12 * result.M21) - result.M01 * (result.M10 * result.M22 - result.M12 * result.M20) + result.M02 * (result.M10 * result.M21 - result.M11 * result.M20);
            if (!float.IsFinite(determinant) || Math.Abs(determinant) < 1e-12f) { throw new ThreeMfFormatException("This 3MF file contains an invalid transform."); }
            return result;
        }
        public Vector3 Apply(Vector3 value) => new(M00 * value.X + M01 * value.Y + M02 * value.Z + M03, M10 * value.X + M11 * value.Y + M12 * value.Z + M13, M20 * value.X + M21 * value.Y + M22 * value.Z + M23);
        public Transform Compose(Transform right) => new(M00 * right.M00 + M01 * right.M10 + M02 * right.M20, M00 * right.M01 + M01 * right.M11 + M02 * right.M21, M00 * right.M02 + M01 * right.M12 + M02 * right.M22, M00 * right.M03 + M01 * right.M13 + M02 * right.M23 + M03, M10 * right.M00 + M11 * right.M10 + M12 * right.M20, M10 * right.M01 + M11 * right.M11 + M12 * right.M21, M10 * right.M02 + M11 * right.M12 + M12 * right.M22, M10 * right.M03 + M11 * right.M13 + M12 * right.M23 + M13, M20 * right.M00 + M21 * right.M10 + M22 * right.M20, M20 * right.M01 + M21 * right.M11 + M22 * right.M21, M20 * right.M02 + M21 * right.M12 + M22 * right.M22, M20 * right.M03 + M21 * right.M13 + M22 * right.M23 + M23);
    }
}
