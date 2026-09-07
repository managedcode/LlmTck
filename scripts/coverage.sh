#!/usr/bin/env bash
set -euo pipefail
mkdir -p artifacts/coverage/raw
dotnet tool run coverlet tests/ManagedCode.LlmTck.Tests/bin/Release/net10.0/ManagedCode.LlmTck.Tests.dll \
  --target dotnet \
  --targetargs "test --project tests/ManagedCode.LlmTck.Tests/ManagedCode.LlmTck.Tests.csproj --configuration Release --no-build --verbosity normal" \
  --format cobertura --format json --output artifacts/coverage/raw/coverage \
  --include '[ManagedCode.LlmTck]*' --include '[ManagedCode.LlmTck.*]*' \
  --exclude '[ManagedCode.LlmTck.Tests]*' --exclude '[ManagedCode.LlmTck.Service]*' --exclude '[ManagedCode.LlmTck.AppHost]*' \
  --threshold 90 --threshold-type line --threshold-stat Total
dotnet tool run reportgenerator -reports:artifacts/coverage/raw/coverage.cobertura.xml \
  -targetdir:artifacts/coverage/report '-reporttypes:HtmlSummary;TextSummary'
