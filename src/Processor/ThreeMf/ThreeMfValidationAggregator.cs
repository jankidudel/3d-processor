using Processor.Models;
using Processor.Reporting;
using Processor.Topology;
using Processor.Validation;

namespace Processor.ThreeMf;

public static class ThreeMfValidationAggregator
{
    public static ValidationResult ValidateBuildItems(IReadOnlyList<Mesh> buildItemMeshes)
    {
        var issueCounts = new Dictionary<(string Code, string Severity), int>();
        var geometry = new MeshGeometryAccumulator();
        var componentCount = 0;
        var openEdgeCount = 0;
        var nonManifoldEdgeCount = 0;
        var inconsistentOrientation = false;

        // Consume one item's bounded result at a time; never retain every item's
        // marker lists or topology indexes while resolving a multi-item build.
        IEnumerable<MeshIssueHighlightAnalysis> AnalyzeItems()
        {
            foreach (var mesh in buildItemMeshes)
            {
                var result = new MeshValidator().Validate(mesh);
                foreach (var issue in result.Issues)
                {
                    var key = (issue.Code, issue.Severity);
                    issueCounts[key] = issueCounts.GetValueOrDefault(key) + issue.Count;
                }
                componentCount += result.Topology.ConnectedComponentCount;
                openEdgeCount += result.Topology.OpenEdgeCount;
                nonManifoldEdgeCount += result.Topology.NonManifoldEdgeCount;
                inconsistentOrientation |= result.Topology.HasInconsistentOrientation;
                if (mesh.Triangles.Count > 0)
                {
                    geometry.Include(result.Geometry!);
                }
                yield return result.IssueAnalysis!;
            }
        }

        var issueAnalysis = MeshIssueHighlightSampler.Combine(AnalyzeItems());
        return new ValidationResult
        {
            Issues = issueCounts.Select(entry => new ValidationIssue(
                entry.Key.Code,
                $"{entry.Key.Code.Replace('_', ' ')} found in 3MF build items.",
                entry.Value,
                entry.Key.Severity)).ToList(),
            IssueAnalysis = issueAnalysis,
            Geometry = geometry.Measurements,
            Topology = new MeshTopology
            {
                ConnectedComponentCount = componentCount,
                OpenEdgeCount = openEdgeCount,
                NonManifoldEdgeCount = nonManifoldEdgeCount,
                HasInconsistentOrientation = inconsistentOrientation
            }
        };
    }
}
