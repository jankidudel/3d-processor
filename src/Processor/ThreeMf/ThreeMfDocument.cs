using Processor.Models;

namespace Processor.ThreeMf;

public sealed record ThreeMfDocument(
    Mesh Mesh,
    IReadOnlyList<Mesh> BuildItemMeshes,
    string SourceUnit,
    int ModelObjectCount,
    int BuildItemCount);
