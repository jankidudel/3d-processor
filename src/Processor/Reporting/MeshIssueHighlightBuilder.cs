using Processor.Models;
using Processor.Validation;

namespace Processor.Reporting;

/// <summary>Stores bounded markers while the authoritative validator scans edges.</summary>
internal sealed class MeshIssueHighlightBuilder
{
    private readonly Dictionary<string, List<MeshIssueHighlight>> samples = [];
    private readonly List<MeshIssueEdgeSegment> openEdges = [];
    private readonly List<MeshIssueEdgeSegment> nonManifoldEdges = [];
    private int openEdgeCount;
    private int nonManifoldEdgeCount;

    public void Add(string code, Vector3 a, Vector3 b, Vector3? c = null)
    {
        if (!samples.TryGetValue(code, out var values))
        {
            values = [];
            samples.Add(code, values);
        }
        if (values.Count < MeshIssueHighlights.SampleLimit)
        {
            values.Add(new MeshIssueHighlight(a, b, c));
        }
    }

    public void AddOpenEdge(Vector3 a, Vector3 b)
    {
        openEdgeCount++;
        Add(ValidationIssueCodes.OpenEdge, a, b);
        AddSegment(openEdges, a, b);
    }

    public void AddNonManifoldEdge(Vector3 a, Vector3 b)
    {
        nonManifoldEdgeCount++;
        Add(ValidationIssueCodes.NonManifoldEdge, a, b);
        AddSegment(nonManifoldEdges, a, b);
    }

    public MeshIssueHighlightAnalysis Build()
    {
        return new MeshIssueHighlightAnalysis(
            new MeshIssueHighlights { Samples = samples },
            new MeshIssueGeometry
            {
                OpenEdges = ToSegments(openEdgeCount, openEdges),
                NonManifoldEdges = ToSegments(nonManifoldEdgeCount, nonManifoldEdges)
            });
    }

    private static void AddSegment(List<MeshIssueEdgeSegment> segments, Vector3 a, Vector3 b)
    {
        if (segments.Count < MeshIssueGeometry.SegmentLimit)
        {
            segments.Add(new MeshIssueEdgeSegment(a, b));
        }
    }

    private static MeshIssueEdgeSegments ToSegments(int count, List<MeshIssueEdgeSegment> segments)
    {
        return new MeshIssueEdgeSegments
        {
            TotalCount = count,
            ReturnedCount = segments.Count,
            Truncated = count > segments.Count,
            Segments = segments
        };
    }
}
