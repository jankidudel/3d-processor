namespace Processor.Validation;

public record ValidationIssue(
    string Code,
    string Message,
    int? TriangleIndex = null
);