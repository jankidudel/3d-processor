using System.Globalization;
using System.IO.Compression;
using System.Xml;
using System.Xml.Linq;

namespace Processor.ThreeMf;

// Rewrites only the relationship-selected model XML. Every other OPC part is copied
// byte-for-byte, including extension resources that this repairer does not interpret.
public sealed class ThreeMfSafeRepairer
{
    public ThreeMfRepairResult Repair(string inputPath, string outputPath)
    {
        using var input = ZipFile.OpenRead(inputPath);
        var modelPart = ModelPart(input);
        var modelEntry = input.GetEntry(modelPart) ?? throw new ThreeMfFormatException("This 3MF file does not contain a supported model part.");
        var document = ReadXml(modelEntry);
        var totals = new Counts();
        foreach (var mesh in document.Descendants().Where(e => e.Name.LocalName == "mesh"))
        {
            totals.ModelsProcessed++;
            RepairMesh(mesh, totals);
        }
        if (totals.ModelsProcessed == 0)
        {
            throw new ThreeMfFormatException("This 3MF file does not contain supported mesh data.");
        }

        using var output = ZipFile.Open(outputPath, ZipArchiveMode.Create);
        foreach (var entry in input.Entries)
        {
            var destination = output.CreateEntry(entry.FullName, CompressionLevel.Optimal);
            using var target = destination.Open();
            if (string.Equals(entry.FullName, modelPart, StringComparison.OrdinalIgnoreCase))
            {
                using var writer = XmlWriter.Create(target, new XmlWriterSettings { Encoding = new System.Text.UTF8Encoding(false), Indent = false });
                document.Save(writer);
            }
            else
            {
                using var source = entry.Open();
                source.CopyTo(target);
            }
        }
        return new ThreeMfRepairResult(totals.ModelsProcessed, totals.Before, totals.After, totals.Degenerate, totals.Duplicates, totals.InvalidIndexes, totals.OrientationFixes);
    }

    private static void RepairMesh(XElement mesh, Counts totals)
    {
        var vertices = mesh.Descendants().Where(e => e.Name.LocalName == "vertex").Select(Vertex.Parse).ToList();
        var triangles = mesh.Descendants().Where(e => e.Name.LocalName == "triangle").Select((element, index) => Face.Parse(element, index)).ToList();
        totals.Before += triangles.Count;
        var retained = new List<Face>();
        var seen = new HashSet<FaceKey>();
        foreach (var triangle in triangles)
        {
            if (!triangle.IsInRange(vertices.Count))
            {
                triangle.Element.Remove();
                totals.InvalidIndexes++;
                continue;
            }
            if (Degenerate(vertices[triangle.A], vertices[triangle.B], vertices[triangle.C]))
            {
                triangle.Element.Remove();
                totals.Degenerate++;
                continue;
            }
            if (!seen.Add(FaceKey.Create(triangle)))
            {
                triangle.Element.Remove();
                totals.Duplicates++;
                continue;
            }
            retained.Add(triangle);
        }
        totals.OrientationFixes += OrientClosedManifoldRegions(retained);
        totals.After += retained.Count;
    }

    private static int OrientClosedManifoldRegions(List<Face> faces)
    {
        var edges = new Dictionary<EdgeKey, List<EdgeUse>>();
        for (var i = 0; i < faces.Count; i++)
        {
            Add(edges, faces[i].A, faces[i].B, i);
            Add(edges, faces[i].B, faces[i].C, i);
            Add(edges, faces[i].C, faces[i].A, i);
        }
        var neighbours = Enumerable.Range(0, faces.Count).Select(_ => new List<(int, EdgeUse, EdgeUse)>()).ToArray();
        var unsafeFaces = new HashSet<int>();
        foreach (var uses in edges.Values)
        {
            if (uses.Count != 2)
            {
                foreach (var use in uses)
                {
                    unsafeFaces.Add(use.Face);
                }

                continue;
            }
            neighbours[uses[0].Face].Add((uses[1].Face, uses[0], uses[1]));
            neighbours[uses[1].Face].Add((uses[0].Face, uses[1], uses[0]));
        }
        var visited = new bool[faces.Count];
        var corrected = 0;
        for (var start = 0; start < faces.Count; start++)
        {
            if (visited[start])
            {
                continue;
            }

            var flips = new Dictionary<int, bool> { [start] = false };
            var component = new List<int>();
            var queue = new Queue<int>();
            queue.Enqueue(start);
            visited[start] = true;
            var safe = true;
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                component.Add(current);
                if (unsafeFaces.Contains(current))
                {
                    safe = false;
                }

                foreach (var neighbour in neighbours[current])
                {
                    var expected = neighbour.Item2.Forward ^ flips[current] ^ neighbour.Item3.Forward ^ true;
                    if (flips.TryGetValue(neighbour.Item1, out var existing))
                    {
                        if (existing != expected)
                        {
                            safe = false;
                        }

                        continue;
                    }
                    flips[neighbour.Item1] = expected;
                    visited[neighbour.Item1] = true;
                    queue.Enqueue(neighbour.Item1);
                }
            }
            if (!safe)
            {
                continue;
            }

            foreach (var index in component.Where(i => flips[i]))
            {
                faces[index].Reverse();
                corrected++;
            }
        }
        return corrected;
    }

    private static bool Degenerate(Vertex a, Vertex b, Vertex c)
    {
        var ab = (b.X - a.X, b.Y - a.Y, b.Z - a.Z);
        var ac = (c.X - a.X, c.Y - a.Y, c.Z - a.Z);
        var bc = (c.X - b.X, c.Y - b.Y, c.Z - b.Z);
        var cross = (ab.Item2 * ac.Item3 - ab.Item3 * ac.Item2, ab.Item3 * ac.Item1 - ab.Item1 * ac.Item3, ab.Item1 * ac.Item2 - ab.Item2 * ac.Item1);
        var area = cross.Item1 * cross.Item1 + cross.Item2 * cross.Item2 + cross.Item3 * cross.Item3;
        var longest = Math.Max(Dot(ab, ab), Math.Max(Dot(ac, ac), Dot(bc, bc)));
        return longest == 0 || area <= 1e-12 * longest * longest;
    }
    private static double Dot((double, double, double) a, (double, double, double) b) => a.Item1 * b.Item1 + a.Item2 * b.Item2 + a.Item3 * b.Item3;
    private static void Add(Dictionary<EdgeKey, List<EdgeUse>> edges, int a, int b, int face)
    {
        var forward = a < b;
        var key = forward ? new EdgeKey(a, b) : new EdgeKey(b, a);
        if (!edges.TryGetValue(key, out var uses))
        {
            edges[key] = uses = [];
        }

        uses.Add(new EdgeUse(face, forward));
    }
    private static string ModelPart(ZipArchive archive)
    {
        var rels = ReadXml(archive.GetEntry("_rels/.rels") ?? throw new ThreeMfFormatException("This 3MF file does not contain a supported model part."));
        var target = rels.Descendants().FirstOrDefault(e => e.Name.LocalName == "Relationship" && ((string?)e.Attribute("Type"))?.EndsWith("/3dmodel", StringComparison.OrdinalIgnoreCase) == true)?.Attribute("Target")?.Value;
        if (string.IsNullOrWhiteSpace(target) || target.Contains("..", StringComparison.Ordinal) || Uri.IsWellFormedUriString(target, UriKind.Absolute))
        {
            throw new ThreeMfFormatException("This 3MF file does not contain a supported model part.");
        }

        return target.TrimStart('/').Replace('\\', '/');
    }
    private static XDocument ReadXml(ZipArchiveEntry entry)
    {
        using var stream = entry.Open();
        using var reader = XmlReader.Create(stream, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 16 * 1024 * 1024 });
        return XDocument.Load(reader);
    }
    private sealed class Counts
    {
        public int ModelsProcessed, Before, After, Degenerate, Duplicates, InvalidIndexes, OrientationFixes;
    }
    private readonly record struct Vertex(double X, double Y, double Z)
    {
        public static Vertex Parse(XElement e) => new(Parse(e, "x"), Parse(e, "y"), Parse(e, "z")); private static double Parse(XElement e, string n) => double.TryParse((string?)e.Attribute(n), NumberStyles.Float, CultureInfo.InvariantCulture, out var v) && double.IsFinite(v) ? v : throw new ThreeMfFormatException("This 3MF file contains invalid model data.");
    }
    private sealed class Face
    {
        public XElement Element
        {
            get;
        }
        public int A
        {
            get; private set;
        }
        public int B
        {
            get; private set;
        }
        public int C
        {
            get; private set;
        }
        private Face(XElement element)
        {
            Element = element;
            A = Number("v1");
            B = Number("v2");
            C = Number("v3");
        }
        private int Number(string name) => int.TryParse((string?)Element.Attribute(name), out var value) ? value : throw new ThreeMfFormatException("This 3MF file contains invalid model data.");
        public static Face Parse(XElement element, int _) => new(element);
        public bool IsInRange(int count) => A >= 0 && B >= 0 && C >= 0 && A < count && B < count && C < count;
        public void Reverse()
        {
            (B, C) = (C, B);
            Element.SetAttributeValue("v2", B);
            Element.SetAttributeValue("v3", C);
        }
    }
    private readonly record struct EdgeKey(int A, int B); private readonly record struct EdgeUse(int Face, bool Forward); private readonly record struct FaceKey(int A, int B, int C)
    {
        public static FaceKey Create(Face f)
        {
            var x = new[] { f.A, f.B, f.C };
            Array.Sort(x);
            return new FaceKey(x[0], x[1], x[2]);
        }
    }
}
public sealed record ThreeMfRepairResult(int ModelsProcessed, int TrianglesBefore, int TrianglesAfter, int DegenerateTrianglesRemoved, int DuplicateTrianglesRemoved, int InvalidIndexTrianglesRemoved, int OrientationFixesApplied);
