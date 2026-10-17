namespace Processor.Reporting;

public sealed class StlSplitReport
{
    public int ReportVersion { get; init; } = 1;
    public string Tool { get; init; } = "stl-splitter";
    public string FileName { get; init; } = "";
    public long FileSizeBytes
    {
        get; init;
    }
    public StlSplitSummary Split { get; init; } = new();
}

public sealed class StlSplitSummary
{
    public string Axis { get; init; } = "Z";
    public double PositionPercent
    {
        get; init;
    }
    public double PositionCoordinate
    {
        get; init;
    }
    public int InputTriangleCount
    {
        get; init;
    }
    public StlSplitPart PartA { get; init; } = new();
    public StlSplitPart PartB { get; init; } = new();
}

public sealed class StlSplitPart
{
    public string FileName { get; init; } = "";
    public int TriangleCount
    {
        get; init;
    }
    public MeshDimensions Dimensions { get; init; } = new();
    public MeshBoundingBox BoundingBox { get; init; } = new();
    public StlSplitValidation Validation { get; init; } = new();
}

public sealed class StlSplitValidation
{
    public bool IsValid
    {
        get; init;
    }
    public bool IsWatertight
    {
        get; init;
    }
    public bool IsManifold
    {
        get; init;
    }
    public int ConnectedComponentCount
    {
        get; init;
    }
}
