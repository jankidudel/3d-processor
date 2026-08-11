# 3d-processor

3D processing libraries.

## Run the application

The processor is currently a .NET console application. Run it from the
repository root and pass the path to an STL file after `--`:

```sh
dotnet run --project src/Processor/Processor.csproj -- data/valid/rotor.stl
```

The `--` separates `dotnet run` options from arguments passed to the
application. For the checked-in `rotor.stl` fixture, the output is:

```text
Triangles: 2356
```

Another checked-in fixture can be used in the same way:

```sh
dotnet run --project src/Processor/Processor.csproj -- data/valid/releaser.stl
```

This prints `Triangles: 1246`. Running without a file prints
`Usage: <file.stl> [--json]` and exits with status code 1.

By default, the application prints a human-readable STL quality report with the
triangle count, totals for each validation issue category, and an overall
validation status. To receive the same summary as structured JSON, add `--json`:

```sh
dotnet run --project src/Processor/Processor.csproj -- data/valid/releaser.stl --json
```

The process exits with status code 0 for a valid mesh and 2 when validation
issues are found.

AWS SAM is not required to run the processor locally. SAM would become useful
if the application were later packaged as an AWS Lambda handler with a SAM
template. Until then, the console entry point provides the simplest local
execution harness.

## Run the tests

The solution file is the test entry point; production `Program.Main` does not need to reference test code.

```sh
dotnet test 3d-processor.slnx
```

### Integration tests

The integration tests read the checked-in binary STL fixtures under `data/valid/`, verify their known triangle counts, and validate the resulting meshes with `StlValidator`.

Run only the real-file integration tests:

```sh
dotnet test 3d-processor.slnx --filter "Category=Integration"
```

Run only the synthetic unit tests:

```sh
dotnet test 3d-processor.slnx --filter "Category!=Integration"
```

The unfiltered `dotnet test` command runs both groups.

## Generate code coverage

```sh
dotnet test 3d-processor.slnx \
  --collect:"XPlat Code Coverage" \
  --results-directory TestResults
```

This writes a Cobertura XML report below `TestResults/`. IDE coverage extensions and tools such as ReportGenerator can read that format.

## Run tests before every commit

Activate the repository's versioned pre-commit hook once after cloning:

```sh
git config core.hooksPath .githooks
```

The hook runs `dotnet test 3d-processor.slnx --no-restore`, including the integration tests. The current fixtures are small enough that running both groups before each commit remains practical. Run `dotnet restore 3d-processor.slnx` at least once after cloning or after package changes.
