namespace Processor.Validation;

/// <summary>Keeps exact counts and only a bounded number of detailed messages per category.</summary>
internal sealed class ValidationIssueCollector
{
    internal const int ExampleLimit = 100;
    private readonly List<ValidationIssue> issues = [];
    private readonly Dictionary<string, int> counts = [];

    public bool WantsExample(string code)
    {
        return counts.GetValueOrDefault(code) < ExampleLimit;
    }

    public void Add(string code, string? example)
    {
        counts[code] = counts.GetValueOrDefault(code) + 1;
        if (example is not null)
        {
            issues.Add(new ValidationIssue(code, example));
        }
    }

    public void Add(ValidationIssue issue)
    {
        issues.Add(issue);
    }

    public List<ValidationIssue> Build()
    {
        foreach (var (code, count) in counts)
        {
            if (count > ExampleLimit)
            {
                issues.Add(new ValidationIssue(
                    code,
                    $"Additional {code.Replace('_', ' ')} findings; detailed examples limited to {ExampleLimit}.",
                    count - ExampleLimit));
            }
        }
        return issues;
    }
}
