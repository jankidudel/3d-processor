namespace Processor.Reporting;

public sealed class StlRepairReport
{
    public MeshQualityReport Before { get; init; } = new();
    public MeshQualityReport? After
    {
        get; init;
    }
    public int DuplicateTrianglesRemoved
    {
        get; init;
    }
    public int DegenerateTrianglesRemoved
    {
        get; init;
    }
    public int OrientationsCorrected
    {
        get; init;
    }
    public IReadOnlyList<string> SkippedReasons { get; init; } = [];
    public bool HasCleanedFile
    {
        get; init;
    }
    public bool IsFullyValid
    {
        get; init;
    }
    public string? CleanedFileName
    {
        get; init;
    }
}
