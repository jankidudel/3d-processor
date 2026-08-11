using System.Text.Json;

namespace Processor.Validation;

public static class ValidationReportFormatter
{
    public static string FormatText(ValidationResult result)
    {
        return $$"""
            STL Quality Report

            Triangles:                {{result.TriangleCount}}
            Degenerate triangles:     {{Count(result, ValidationIssueCodes.DegenerateTriangle)}}
            Invalid coordinates:      {{Count(result, ValidationIssueCodes.InvalidCoordinates)}}
            Duplicate triangles:      {{Count(result, ValidationIssueCodes.DuplicateTriangle)}}
            Open edges:               {{Count(result, ValidationIssueCodes.OpenEdge)}}
            Non-manifold edges:       {{Count(result, ValidationIssueCodes.NonManifoldEdge)}}
            Inconsistent orientation: {{Count(result, ValidationIssueCodes.InconsistentOrientation)}}

            Validation: {{(result.IsValid ? "OK" : "FAIL")}}
            """;
    }

    public static string FormatJson(ValidationResult result)
    {
        var report = new
        {
            valid = result.IsValid,
            triangleCount = result.TriangleCount,
            issues = new
            {
                degenerateTriangles = Count(result, ValidationIssueCodes.DegenerateTriangle),
                invalidCoordinates = Count(result, ValidationIssueCodes.InvalidCoordinates),
                duplicateTriangles = Count(result, ValidationIssueCodes.DuplicateTriangle),
                openEdges = Count(result, ValidationIssueCodes.OpenEdge),
                nonManifoldEdges = Count(result, ValidationIssueCodes.NonManifoldEdge),
                inconsistentOrientation = Count(result, ValidationIssueCodes.InconsistentOrientation)
            }
        };

        return JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true });
    }

    private static int Count(ValidationResult result, string code)
    {
        return result.CountIssues(code);
    }
}
