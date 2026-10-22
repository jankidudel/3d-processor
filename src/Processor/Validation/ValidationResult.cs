using Processor.Topology;
using Processor.Reporting;

namespace Processor.Validation;

public class ValidationResult
{
    // Only bounded output is retained, never the large analysis dictionaries or mesh.
    internal MeshIssueHighlightAnalysis? IssueAnalysis
    {
        get; init;
    }
    internal MeshGeometryMeasurements? Geometry
    {
        get; init;
    }
    public List<ValidationIssue> Issues { get; init; } = [];
    public MeshTopology Topology { get; init; } = new();

    public bool IsValid => Issues.All(issue => issue.Severity != ValidationIssueSeverity.Error);

    public int CountIssues(string code)
    {
        return Issues
            .Where(issue => issue.Code == code)
            .Sum(issue => issue.Count);
    }
}
