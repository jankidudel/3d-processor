using Processor.Models;

namespace Processor.Validation;

public class StlValidator
{
    public ValidationResult Validate(Mesh mesh)
    {
        var errors = new List<string>();

        if (mesh.Triangles.Count == 0)
        {
            errors.Add("Mesh contains no triangles.");
        }

        for (var i = 0; i < mesh.Triangles.Count; i++)
        {
            var triangle = mesh.Triangles[i];

            if (!IsValidVector(triangle.A) ||
                !IsValidVector(triangle.B) ||
                !IsValidVector(triangle.C))
            {
                errors.Add($"Triangle {i} contains invalid coordinates.");
            }

            if (IsDegenerate(triangle))
            {
                errors.Add($"Triangle {i} is degenerate.");
            }
        }

        return new ValidationResult
        {
            IsValid = errors.Count == 0,
            Errors = errors
        };
    }

    private static bool IsValidVector(Vector3 vector)
    {
        return float.IsFinite(vector.X) &&
               float.IsFinite(vector.Y) &&
               float.IsFinite(vector.Z);
    }

    private static bool IsDegenerate(Triangle triangle)
    {
        return triangle.A == triangle.B ||
               triangle.A == triangle.C ||
               triangle.B == triangle.C;
    }
}