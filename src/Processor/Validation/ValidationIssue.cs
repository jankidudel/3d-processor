namespace Processor.Validation;

public record ValidationIssue(
    string Code,
    string Message,
    int Count = 1,
    string Severity = ValidationIssueSeverity.Error
);
