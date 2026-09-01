using Processor.Models;

namespace Processor.Reporting;

public sealed class MeshIssueGeometry
{
    public const int SegmentLimit = 5_000;

    public string Version { get; init; } = "1.0";
    public MeshIssueEdgeSegments? OpenEdges { get; init; }
    public MeshIssueEdgeSegments? NonManifoldEdges { get; init; }
}

public sealed class MeshIssueEdgeSegments
{
    public int TotalCount { get; init; }
    public int ReturnedCount { get; init; }
    public bool Truncated { get; init; }
    public List<MeshIssueEdgeSegment> Segments { get; init; } = [];
}

public sealed record MeshIssueEdgeSegment(Vector3 Start, Vector3 End);
