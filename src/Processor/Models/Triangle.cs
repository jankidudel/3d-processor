namespace Processor.Models;

public readonly record struct Triangle(
  Vector3 Normal,
  Vector3 A,
  Vector3 B,
  Vector3 C
);
