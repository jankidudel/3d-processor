# STL integration fixtures

The binary STL fixtures in this directory support real-file integration tests.
The malformed fixtures are derived from `valid/rotor.stl` with one controlled
mutation:

- `degenerate/`: collapses one triangle's third vertex onto its first.
- `duplicate-triangles/`: replaces one triangle with a copy of another.
- `inconsistent-orientation/`: reverses one triangle's vertex order.
- `invalid-coordinates/`: replaces one vertex coordinate with `NaN`.
- `open-edges/`: removes one triangle from the closed mesh.
- `non-manifold/`: appends a copy of one triangle, giving its edges three uses.
- `corrupt/`: truncates the final triangle while retaining the original header
  count, exercising binary STL processing failures.

The `self-intersection/` directory is reserved for fixtures once a
self-intersection validation rule is implemented. The current validator does
not detect triangle intersections, so adding an expectation for that category
would be misleading.

The ASCII fixtures in `connected-components/` cover connected-component
reporting: one shell, two and three separate closed shells, and two closed
shells touching at exactly one vertex. The last case is one component because
the topology rule treats shared vertices as connected.
