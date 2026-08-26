using Processor.Topology;
using Processor.Validation;

namespace Processor.ThreeMf;

public static class ThreeMfValidationAggregator
{
    public static ValidationResult ValidateBuildItems(IReadOnlyList<Models.Mesh> buildItemMeshes)
    {
        var results = buildItemMeshes.Select(mesh => new StlValidator().Validate(mesh)).ToList();
        var issues = results
            .SelectMany(result => result.Issues)
            .GroupBy(issue => new { issue.Code, issue.Severity })
            .Select(group => new ValidationIssue(
                group.Key.Code,
                $"{group.Key.Code.Replace('_', ' ')} found in 3MF build items.",
                group.Sum(issue => issue.Count),
                group.Key.Severity))
            .ToList();
        return new ValidationResult
        {
            Issues = issues,
            Topology = new MeshTopology
            {
                ConnectedComponentCount = results.Sum(result => result.Topology.ConnectedComponentCount),
                OpenEdgeCount = results.Sum(result => result.Topology.OpenEdgeCount),
                NonManifoldEdgeCount = results.Sum(result => result.Topology.NonManifoldEdgeCount),
                HasInconsistentOrientation = results.Any(result => result.Topology.HasInconsistentOrientation)
            }
        };
    }
}
