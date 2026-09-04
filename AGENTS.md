# 3D Processor

## Purpose

This repository contains the backend 3D model processing engine.

The initial focus is STL processing and validation, but the architecture
should not unnecessarily assume STL is the only supported format.

## Technology

- .NET
- C#
- xUnit for tests
- Solution format: .slnx

## Architecture

Production code lives under `src/`.
Tests live under `tests/`.

Keep parsing, geometry models, validation, and application orchestration
separated where practical.

Prefer small focused classes with clear responsibilities.

Use interfaces where they provide a meaningful abstraction, such as
supporting multiple file readers or implementations. Do not create
interfaces purely for the sake of having interfaces.

## Coding guidelines

- Prefer simple, readable C# over clever abstractions.
- Use explicit `{}` blocks for every `if`, `else`, loop, and other control-flow
  body, including single-statement bodies. Do not write braceless control flow.
- Put every control-flow condition, opening brace, body statement, and closing
  brace on its own appropriate line. Never compress an `if`, `else`, loop,
  `switch` arm, `return`, or other statement into a single line.
- Every `if` and `else` body must use braces, even for exactly one statement.
  For example, write `if (condition) { ... } else { ... }` as multiline blocks;
  never write an unbraced `if (condition) statement;` or `else statement;`.
- Enable nullable reference types.
- Use modern C# features where they improve readability.
- Prefer immutable models where practical.
- Avoid unnecessary dependencies.
- Avoid unnecessary allocations in performance-sensitive mesh processing.
- Do not introduce Python.
- Do not introduce external services unless explicitly requested.

## Testing

Every new behaviour should have automated tests where practical.

Run:

    dotnet test 3d-processor.slnx --nologo

before considering a change complete.

Tests must not depend on network access.

Test STL files belong under the test data directory rather than production code.

## Changes

Before adding a new dependency, consider whether the .NET standard library
already provides what is required.

Do not make unrelated refactors while implementing a requested feature.

Keep changes small and easy to review.
