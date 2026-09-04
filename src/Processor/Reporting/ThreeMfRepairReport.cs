namespace Processor.Reporting;

public sealed class ThreeMfRepairReport
{
    public int ReportVersion { get; init; } = 1;
    public string Tool { get; init; } = "3mf-repair";
    public string FileName { get; init; } = "";
    public long FileSizeBytes
    {
        get; init;
    }
    public ThreeMfRepairSummary Repair { get; init; } = new();
    public MeshQualityReport BeforeValidation { get; init; } = new();
    public MeshQualityReport? AfterValidation
    {
        get; init;
    }
    public IReadOnlyList<string> Warnings { get; init; } = [];
}
public sealed class ThreeMfRepairSummary
{
    public bool RepairedFileAvailable
    {
        get; init;
    }
    public int ModelsProcessed
    {
        get; init;
    }
    public int TrianglesBefore
    {
        get; init;
    }
    public int TrianglesAfter
    {
        get; init;
    }
    public int DegenerateTrianglesRemoved
    {
        get; init;
    }
    public int DuplicateTrianglesRemoved
    {
        get; init;
    }
    public int InvalidIndexTrianglesRemoved
    {
        get; init;
    }
    public int OrientationFixesApplied
    {
        get; init;
    }
}
