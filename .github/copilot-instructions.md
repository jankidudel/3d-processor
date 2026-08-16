# GitHub Copilot Instructions

Follow the repository architecture and development guidelines documented
in `AGENTS.md`.

When generating C#:

- Prefer simple and maintainable implementations.
- Use explicit `{}` blocks for every `if`, `else`, loop, and other control-flow
  body, including single-statement bodies. Do not write braceless control flow.
- Follow the existing naming and namespace conventions.
- Do not add dependencies unless necessary.
- Do not introduce Python.
- Add or update tests when behaviour changes.
- Do not modify unrelated code.

Before completing implementation work, ensure:

    dotnet test 3d-processor.slnx --nologo

passes.
