namespace Processor.Validation;

public class ValidationResult
{
    public bool IsValid { get; init; }

    public List<string> Errors { get; init; } = [];
}