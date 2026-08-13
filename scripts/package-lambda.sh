#!/bin/sh
set -eu

repo_dir=$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)
artifact_dir="$repo_dir/artifacts/lambda"

dotnet test "$repo_dir/3d-processor.slnx" --nologo
dotnet publish "$repo_dir/src/Processor/Processor.csproj" \
  --configuration Release \
  --output "$artifact_dir" \
  --nologo

echo "Lambda artifact created at $artifact_dir"
