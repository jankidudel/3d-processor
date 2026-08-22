using Processor.Models;

namespace Processor.Topology;

/// <summary>
/// Finds triangle components by shared vertex. This intentionally treats
/// vertex-only contact as connected, as well as shared edges.
/// </summary>
public static class MeshTopologyAnalyzer
{
    public static MeshTopology Analyze(Mesh mesh)
    {
        var triangleCount = mesh.Triangles.Count;
        if (triangleCount == 0)
        {
            return new MeshTopology();
        }

        var components = new DisjointSet(triangleCount);
        var firstTriangleByVertex = new Dictionary<Vector3, int>();

        for (var triangleIndex = 0; triangleIndex < triangleCount; triangleIndex++)
        {
            var triangle = mesh.Triangles[triangleIndex];
            ConnectVertex(triangle.A, triangleIndex, firstTriangleByVertex, components);
            ConnectVertex(triangle.B, triangleIndex, firstTriangleByVertex, components);
            ConnectVertex(triangle.C, triangleIndex, firstTriangleByVertex, components);
        }

        return new MeshTopology
        {
            ConnectedComponentCount = components.CountRoots()
        };
    }

    private static void ConnectVertex(
        Vector3 vertex,
        int triangleIndex,
        Dictionary<Vector3, int> firstTriangleByVertex,
        DisjointSet components)
    {
        if (firstTriangleByVertex.TryGetValue(vertex, out var firstTriangleIndex))
        {
            components.Union(firstTriangleIndex, triangleIndex);
            return;
        }

        firstTriangleByVertex.Add(vertex, triangleIndex);
    }

    private sealed class DisjointSet(int count)
    {
        private readonly int[] _parents = Enumerable.Range(0, count).ToArray();
        private readonly byte[] _ranks = new byte[count];

        public void Union(int first, int second)
        {
            var firstRoot = Find(first);
            var secondRoot = Find(second);
            if (firstRoot == secondRoot)
            {
                return;
            }

            if (_ranks[firstRoot] < _ranks[secondRoot])
            {
                _parents[firstRoot] = secondRoot;
                return;
            }

            _parents[secondRoot] = firstRoot;
            if (_ranks[firstRoot] == _ranks[secondRoot])
            {
                _ranks[firstRoot]++;
            }
        }

        public int CountRoots()
        {
            var count = 0;
            for (var index = 0; index < _parents.Length; index++)
            {
                if (Find(index) == index)
                {
                    count++;
                }
            }

            return count;
        }

        private int Find(int value)
        {
            var root = value;
            while (_parents[root] != root)
            {
                root = _parents[root];
            }

            while (_parents[value] != value)
            {
                var parent = _parents[value];
                _parents[value] = root;
                value = parent;
            }

            return root;
        }
    }
}
