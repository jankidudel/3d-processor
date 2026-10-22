namespace Processor.Models;

public class Mesh(int triangleCapacity = 0)
{
    public List<Triangle> Triangles { get; } = new(triangleCapacity);
}
