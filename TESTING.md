# Manually running tests

Run test commands from the repository root, where `3d-processor.slnx` is located.
The test suite uses xUnit and requires the .NET SDK version that supports the
projects' `net10.0` target framework.

Restore dependencies after cloning the repository or changing package references:

```sh
dotnet restore 3d-processor.slnx
```

## Run all tests

Run both synthetic unit tests and integration tests:

```sh
dotnet test 3d-processor.slnx --nologo
```

## Run synthetic unit tests

Synthetic tests construct their test data in code and do not have the
`Integration` category. Run them without the real-file integration tests:

```sh
dotnet test 3d-processor.slnx --nologo --filter "Category!=Integration"
```

## Run integration tests

Integration tests read the checked-in STL fixtures under `data/valid/`. Run only
tests marked with the xUnit `Category=Integration` trait:

```sh
dotnet test 3d-processor.slnx --nologo --filter "Category=Integration"
```

## Useful options

Show the name and result of each test while troubleshooting:

```sh
dotnet test 3d-processor.slnx --nologo --verbosity normal
```

Skip package restore when dependencies are already restored:

```sh
dotnet test 3d-processor.slnx --nologo --no-restore
```

All tests are deterministic, require no network access while running, and can be
run directly from a terminal without starting the application.
