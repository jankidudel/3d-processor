using Processor.Validation;

namespace Processor.Reporting;

/// <summary>A compact, versioned report for the dedicated 3MF Quality Checker.</summary>
public sealed class ThreeMfQualityCheckerReport
{
    public int ReportVersion { get; init; } = 1;
    public string Tool { get; init; } = "3mf-quality-checker";
    public string FileName { get; init; } = "";
    public long FileSizeBytes { get; init; }
    public bool IsValid { get; init; }
    public ThreeMfQualitySummary Summary { get; init; } = new();
    public ValidationResult Validation { get; init; } = new();
    public IReadOnlyList<ThreeMfQualityModelReport> Models { get; init; } = [];
}

public sealed class ThreeMfQualitySummary
{
    public int ResolvedModelCount { get; init; }
    public int TriangleCount { get; init; }
    public int ErrorCount { get; init; }
    public int WarningCount { get; init; }
}

public sealed class ThreeMfQualityModelReport
{
    public int ModelIndex { get; init; }
    public string Name { get; init; } = "";
    public int TriangleCount { get; init; }
    public MeshDimensions Dimensions { get; init; } = new();
    public string Unit { get; init; } = "millimeter";
    public ValidationResult Validation { get; init; } = new();
}
