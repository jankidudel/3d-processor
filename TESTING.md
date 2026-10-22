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

Integration tests read the checked-in STL fixtures under `tests/TestData/stl/`. Run only
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

## Run the performance smoke test

The performance smoke test generates temporary approximately 1 MB, 20 MB, and
100 MB closed binary STL files. It reports processing time, working-set growth,
peak (or current, where peak is unavailable) working set, managed heap size,
live managed memory, and triangle count. It is opt-in so normal test runs remain
fast:

```sh
RUN_PERFORMANCE_SMOKE_TESTS=1 dotnet test 3d-processor.slnx \
  --nologo \
  --filter "Category=Performance" \
  --logger "console;verbosity=detailed"
```

The generated files are deleted after the test and are not committed to the
repository. The smoke test is intended to expose major regressions; it does not
replace repeatable benchmarking.

## Repeated processing and memory regressions

The normal test suite generates a 281,540-triangle binary STL and processes it
twice in one process without forcing collection between calls. It checks stable
reports and a broad 128 MiB allocation budget per call. The generated mesh is
owned mathematical geometry, not a customer upload. Additional tests verify
bounded diagnostic messages, exact issue totals, independent 3MF build items and
agreement between validation and overlays, including triangles in the YZ plane.

Measure an existing STL or 3MF using the complete processing/reporting pipeline:

```sh
dotnet run --configuration Release --project tools/MemoryProbe -- /absolute/path/model.stl
```

The probe runs the file twice without forced garbage collection and reports
elapsed time, total allocations and process working set sampled every 2 ms.
Allocations are cumulative bytes allocated during a call, not peak live memory.
Sampling can miss brief peaks. These measurements include the local .NET host,
but exclude the Lambda host and AWS request overhead; they are not proof of a
production memory ceiling.

After building the probe, exercise a constrained managed heap:

```sh
DOTNET_GCHeapHardLimit=0x08000000 dotnet tools/MemoryProbe/bin/Release/net10.0/MemoryProbe.dll /absolute/path/model.stl
```

This sets a 128 MiB managed GC budget for that local process. It does not impose
Lambda's 256 MB total-process limit or simulate the Linux Lambda runtime.
The probe accepts existing files without copying them into the repository.
