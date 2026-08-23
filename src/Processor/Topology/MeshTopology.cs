namespace Processor.Topology;

public sealed class MeshTopology
{
    public int ConnectedComponentCount { get; init; }
    public int OpenEdgeCount { get; init; }
    public int NonManifoldEdgeCount { get; init; }
    public bool HasInconsistentOrientation { get; init; }
}
