# 3d-processor

3D processing engine with a command-line interface and an AWS Lambda SQS/S3 adapter.

## Run the application

The processor is currently a .NET console application. Run it from the
repository root and pass the path to an STL file after `--`:

```sh
dotnet run --project src/Processor/Processor.csproj -- tests/TestData/stl/valid/rotor.stl
```

The `--` separates `dotnet run` options from arguments passed to the
application. For the checked-in `rotor.stl` fixture, the output is:

```text
STL Quality Report

File:                     rotor.stl
File size:                117884 bytes
Triangles:                2356
...
Validation: OK
```

Another checked-in fixture can be used in the same way:

```sh
dotnet run --project src/Processor/Processor.csproj -- tests/TestData/stl/valid/releaser.stl
```

This reports `1246` triangles. Running without a file prints
`Usage: <file.stl> [--json]` and exits with status code 1.

By default, the application prints a human-readable STL quality report with the
triangle count, totals for each validation issue category, and an overall
validation status. To receive the same summary as structured JSON, add `--json`:

```sh
dotnet run --project src/Processor/Processor.csproj -- tests/TestData/stl/valid/releaser.stl --json
```

The process exits with status code 0 for a valid mesh and 2 when validation
issues are found. Processing failures, such as missing or truncated files, write
to standard error and exit with status code 1. In `--json` mode, standard output
therefore contains either one complete JSON report or nothing, never diagnostic
text mixed with JSON.

The versioned JSON report includes the source file name and size, triangle
count, axis-aligned bounding box, width/height/depth, surface area, volume, and
structured validation results. This report is the processor's output model and
can be consumed directly by API or UI layers.

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

The integration tests read the checked-in binary STL fixtures under `tests/TestData/`,
verify known metadata and validation results, and exercise corrupt-file handling.

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

The hook runs `dotnet format 3d-processor.slnx --no-restore` and then
`dotnet test 3d-processor.slnx --no-restore`, including the integration tests.
If formatting changes files, the commit is stopped so you can review and stage
them. The current fixtures are small enough that running both groups before each
commit remains practical. Run `dotnet restore 3d-processor.slnx` at least once
after cloning or after package changes.

## Package for AWS Lambda

Install the official AWS Lambda packaging CLI once:

```sh
dotnet tool install -g Amazon.Lambda.Tools
```

If it is already installed, update it with:

```sh
dotnet tool update -g Amazon.Lambda.Tools
```

The tool is a developer prerequisite; it is not installed during every build.

From the repository root, use:

```sh
make build    # Release build targeting net10.0
make test     # run the complete solution test suite
make package  # create artifacts/3d-processor.zip
make clean    # remove .NET build output and artifacts/
```

`make package` uses `Amazon.Lambda.Tools` to compile and package only the published
Lambda application and its runtime dependencies. It replaces any existing ZIP at
`artifacts/3d-processor.zip`; it does not deploy anything and requires no AWS
credentials. The generated artifact is ignored by Git and is consumed by the
separate `3d-infra` repository as a CDK Lambda asset.

`scripts/package-lambda.sh` remains as a compatibility wrapper and delegates to
`make package`.

## AWS Lambda worker

The Lambda handler is:

```text
Processor::Processor.Function::FunctionHandler
```

It receives SQS messages containing native S3 object-created events. For each
`jobs/{jobId}/input/input.stl` object, it downloads the file to Lambda's temporary
storage, runs the existing validation engine, and writes JSON to
`jobs/{jobId}/output/report.json`. The same report key is overwritten on duplicate
delivery, making retries deterministic without introducing a database.

The worker currently accepts `.stl` files. Unsupported formats fail the SQS record
so normal retry and DLQ handling apply. It reports individual SQS failures using
Lambda partial-batch responses.

Uploads larger than 25 MiB are rejected using both the AWS-generated S3 event size
and a fresh S3 metadata check immediately before download. The second check prevents
an oversized object replacement from bypassing the event-size validation.

When the third receive attempt fails, the worker also writes a safe terminal result
to `jobs/{jobId}/output/failure.json` before the message moves to the DLQ. The API uses
that artifact to return `status: "failed"` without a database.

The infrastructure supplies these environment variables:

```text
STORAGE_BUCKET_NAME
JOBS_PREFIX
```

The CLI remains available and continues to use the same processing and validation
engine as the Lambda adapter.
