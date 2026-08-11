# 3d-processor

3D processing libraries.

## Run the application

```sh
dotnet run --project src/Processor/Processor.csproj -- data/valid/rotor.stl
```

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
