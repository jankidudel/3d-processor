# Deterministic 3MF repair fixtures

These small 3MF packages are generated locally, committed for browser and
processor testing, and are not deployed as website assets. Run
`python3 generate-fixtures.py` from this directory to reproduce them exactly.
Each package contains standard content types, a root relationship, and
`3D/model.model`; only its described mesh defect is intentional.

| File | Intentional defect | Quality Checker result | Repair result |
| --- | --- | --- | --- |
| `valid-closed-cube.3mf` | None; a 12-triangle closed cube. | Valid with no warnings or errors. | **No repairs needed**; zero repairs and no repaired-file download. |
| `degenerate-triangles.3mf` | One zero-area triangle appended to the cube. | Degenerate-triangle issue. | Degenerate triangle removed; 13 triangles before, 12 after; valid output. |
| `duplicate-triangles.3mf` | One exact duplicate of a cube face. | Duplicate-triangle issue (and topology side effects caused by the duplicate). | Duplicate removed; 13 triangles before, 12 after; valid output. |
| `flipped-orientation.3mf` | One cube face has reversed winding. | Inconsistent-orientation issue. | Orientation corrections applied; 12 triangles before and after; valid output. |
| `open-mesh.3mf` | One cube face is missing. | Open-edge/non-watertight issue. | **No safe repairs could be applied**; no output package is created. |
| `non-manifold-edge.3mf` | An added triangle makes three faces share one cube edge. | Non-manifold-edge issue. | No topology-changing repair; no output package is created. |
| `combined-safe-and-unsafe-issues.3mf` | A missing cube face plus one duplicate and one degenerate triangle. | Open-edge plus duplicate/degenerate issues. | Safe faces are removed; 13 triangles before, 11 after; open-edge warning remains. |
| `multi-object-with-metadata.3mf` | Two closed cube objects, with a duplicate only in object 2. The model also carries a unit, metadata, build items, and a basic material resource. | Duplicate-triangle issue in the affected build item. | Only object 2 changes; 25 triangles before, 24 after; object IDs, build items, unit, metadata, and resource remain. |

The fixtures deliberately do not cover malformed ZIPs, invalid XML, or broken
package references. Those belong in the separate reader failure fixtures.
