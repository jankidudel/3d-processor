namespace Processor.Validation;

public class ValidationResult
{
    public List<ValidationIssue> Issues { get; init; } = [];

    public bool IsValid => Issues.Count == 0;

    public int CountIssues(string code)
    {
        return Issues
            .Where(issue => issue.Code == code)
            .Sum(issue => issue.Count);
    }
}
