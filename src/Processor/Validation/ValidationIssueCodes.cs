namespace Processor.Validation;

public static class ValidationIssueCodes
{
    public const string EmptyMesh = "empty_mesh";
    public const string DegenerateTriangle = "degenerate_triangle";
    public const string InvalidCoordinates = "invalid_coordinates";
    public const string DuplicateTriangle = "duplicate_triangle";
    public const string OpenEdge = "open_edge";
    public const string NonManifoldEdge = "non_manifold_edge";
    public const string InconsistentOrientation = "inconsistent_orientation";
    public const string MultipleDisconnectedShells = "multiple_disconnected_shells";
}
