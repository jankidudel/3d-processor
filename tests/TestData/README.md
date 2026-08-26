# Test data

This directory contains versioned test fixtures only. Production code must not
depend on anything here. Fixtures are grouped by format (`stl/`, `3mf/`) and
then by purpose, so a new format can be added without changing the overall
layout.

## Fixture size budget

- Keep ordinary checked-in fixtures at or below **250 KiB** each.
- Keep the complete versioned fixture set at or below **10 MiB**.
- Generate large files in tests or benchmarks and delete them afterwards; do
  not commit them.
- An exception needs a documented reason in the relevant fixture manifest and
  agreement during review.

This keeps clones, CI, and test execution lightweight. These files are not
included in the Lambda publish project, so they do not affect its deployment
artifact, cold starts, or runtime memory use.

## External corpus fixtures

Put externally obtained, valid real-world files in
`<format>/corpus/`. Add a `README.md` next to each file using
[`CORPUS-FIXTURE-TEMPLATE.md`](CORPUS-FIXTURE-TEMPLATE.md). Do not add a file
whose source and licence are unknown.

## Current STL fixtures

The binary malformed fixtures are derived from `stl/valid/rotor.stl` with one
controlled mutation:

- `degenerate/`: collapses one triangle's third vertex onto its first.
- `duplicate-triangles/`: replaces one triangle with a copy of another.
- `inconsistent-orientation/`: reverses one triangle's vertex order.
- `invalid-coordinates/`: replaces one vertex coordinate with `NaN`.
- `open-edges/`: removes one triangle from the closed mesh.
- `non-manifold/`: appends a copy of one triangle, giving its edges three uses.
- `corrupt/`: truncates the final triangle while retaining the original header
  count.

`stl/self-intersection/` is reserved until such validation exists. The ASCII
fixtures in `stl/connected-components/` exercise topology reporting.
