using Processor.Models;

namespace Processor.Stl;

public interface IStlReader
{
    Mesh Read(string filePath);
}
