using Processor.Models;
using Processor.Validation;

namespace Processor.Reporting;

public sealed class MeshIssueHighlights
{
    public const int SampleLimit = 100;
    public Dictionary<string, List<MeshIssueHighlight>> Samples { get; init; } = [];
}

public sealed record MeshIssueHighlight(Vector3 A, Vector3 B, Vector3? C = null);

internal static class MeshIssueHighlightSampler
{
    public static MeshIssueHighlights Sample(Mesh mesh)
    {
        return Analyze(mesh).Highlights;
    }

    public static MeshIssueHighlightAnalysis Analyze(Mesh mesh)
    {
        return new MeshValidator().Validate(mesh).IssueAnalysis!;
    }

    /// <summary>
    /// Samples every supplied mesh independently. Coincident edges in separate
    /// meshes remain separate diagnostic markers, matching aggregate topology
    /// reports that validate each mesh independently.
    /// </summary>
    public static MeshIssueHighlightAnalysis Analyze(IEnumerable<Mesh> meshes)
    {
        return Combine(meshes.Select(Analyze));
    }

    internal static MeshIssueHighlightAnalysis Combine(IEnumerable<MeshIssueHighlightAnalysis> analyses)
    {
        var highlights = new Dictionary<string, List<MeshIssueHighlight>>();
        var openEdges = new List<MeshIssueEdgeSegment>();
        var nonManifoldEdges = new List<MeshIssueEdgeSegment>();
        var openEdgeCount = 0;
        var nonManifoldEdgeCount = 0;

        foreach (var analysis in analyses)
        {
            AppendHighlights(highlights, analysis.Highlights.Samples);
            AppendSegments(openEdges, analysis.Geometry.OpenEdges, ref openEdgeCount);
            AppendSegments(nonManifoldEdges, analysis.Geometry.NonManifoldEdges, ref nonManifoldEdgeCount);
        }

        return new MeshIssueHighlightAnalysis(
            new MeshIssueHighlights { Samples = highlights },
            new MeshIssueGeometry
            {
                OpenEdges = ToSegments(openEdgeCount, openEdges),
                NonManifoldEdges = ToSegments(nonManifoldEdgeCount, nonManifoldEdges)
            });
    }

    private static void AppendHighlights(
        Dictionary<string, List<MeshIssueHighlight>> destination,
        Dictionary<string, List<MeshIssueHighlight>> source)
    {
        foreach (var (code, samples) in source)
        {
            foreach (var sample in samples)
            {
                Add(destination, code, sample);
            }
        }
    }

    private static void AppendSegments(
        List<MeshIssueEdgeSegment> destination,
        MeshIssueEdgeSegments? source,
        ref int totalCount)
    {
        if (source is null)
        {
            return;
        }
        totalCount += source.TotalCount;
        foreach (var segment in source.Segments)
        {
            if (destination.Count < MeshIssueGeometry.SegmentLimit)
            {
                destination.Add(segment);
            }
        }
    }

    private static MeshIssueEdgeSegments ToSegments(int totalCount, List<MeshIssueEdgeSegment> segments)
    {
        return new MeshIssueEdgeSegments
        {
            TotalCount = totalCount,
            ReturnedCount = segments.Count,
            Truncated = totalCount > segments.Count,
            Segments = segments
        };
    }

    private static void Add(Dictionary<string, List<MeshIssueHighlight>> samples, string code, MeshIssueHighlight sample)
    {
        if (!samples.TryGetValue(code, out var values))
        {
            values = [];
            samples.Add(code, values);
        }
        if (values.Count < MeshIssueHighlights.SampleLimit)
        {
            values.Add(sample);
        }
    }
}

internal sealed record MeshIssueHighlightAnalysis(
    MeshIssueHighlights Highlights,
    MeshIssueGeometry Geometry);
