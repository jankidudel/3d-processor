using Processor.Topology;

namespace Processor.Validation;

public class ValidationResult
{
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
