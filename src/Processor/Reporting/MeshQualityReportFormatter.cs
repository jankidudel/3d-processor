using System.Globalization;
using System.Text.Json;
using Processor.Validation;

namespace Processor.Reporting;

public static class MeshQualityReportFormatter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public static string FormatText(MeshQualityReport report)
    {
        return FormattableString.Invariant($$"""
            STL Quality Report

            File:                     {{report.FileName}}
            File size:                {{report.FileSizeBytes}} bytes
            Triangles:                {{report.TriangleCount}}
            Bounding box min:         ({{Format(report.BoundingBox.MinX)}}, {{Format(report.BoundingBox.MinY)}}, {{Format(report.BoundingBox.MinZ)}})
            Bounding box max:         ({{Format(report.BoundingBox.MaxX)}}, {{Format(report.BoundingBox.MaxY)}}, {{Format(report.BoundingBox.MaxZ)}})
            Dimensions (W x H x D):   {{Format(report.Dimensions.Width)}} x {{Format(report.Dimensions.Height)}} x {{Format(report.Dimensions.Depth)}}
            Surface area:             {{Format(report.SurfaceArea)}}
            Volume:                   {{Format(report.Volume)}}
            Connected components:     {{report.Topology.ConnectedComponentCount}}

            Degenerate triangles:     {{Count(report, ValidationIssueCodes.DegenerateTriangle)}}
            Invalid coordinates:      {{Count(report, ValidationIssueCodes.InvalidCoordinates)}}
            Duplicate triangles:      {{Count(report, ValidationIssueCodes.DuplicateTriangle)}}
            Open edges:               {{Count(report, ValidationIssueCodes.OpenEdge)}}
            Non-manifold edges:       {{Count(report, ValidationIssueCodes.NonManifoldEdge)}}
            Inconsistent orientation: {{Count(report, ValidationIssueCodes.InconsistentOrientation)}}
            Disconnected shells:      {{Count(report, ValidationIssueCodes.MultipleDisconnectedShells)}}

            Validation: {{(report.Validation.IsValid ? "OK" : "FAIL")}}
            """);
    }

    public static string FormatJson(MeshQualityReport report)
    {
        return JsonSerializer.Serialize(report, JsonOptions);
    }

    public static string FormatJson(StlRepairReport report)
    {
        return JsonSerializer.Serialize(report, JsonOptions);
    }

    public static string FormatJson(object report) => JsonSerializer.Serialize(report, JsonOptions);

    private static int Count(MeshQualityReport report, string code)
    {
        return report.Validation.CountIssues(code);
    }

    private static string Format(double value)
    {
        return value.ToString("G10", CultureInfo.InvariantCulture);
    }
}
