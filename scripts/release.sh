#!/usr/bin/env bash
set -euo pipefail
case "${1:?Expected resolve or publish}" in
  resolve)
    version=$(dotnet msbuild src/ManagedCode.LlmTck/ManagedCode.LlmTck.csproj -nologo -v:q -getProperty:Version)
    tag="v$version"
    echo "version=$version" >> "$GITHUB_OUTPUT"
    echo "tag=$tag" >> "$GITHUB_OUTPUT"
    if gh release view "$tag" --json assets --jq '.assets[].name' 2>/dev/null | grep -qx 'nuget-published.txt' && [ "${FORCE_RELEASE:-false}" != true ]; then
      echo 'should_release=false' >> "$GITHUB_OUTPUT"
    else
      echo 'should_release=true' >> "$GITHUB_OUTPUT"
    fi
    ;;
  publish)
    test -n "${NUGET_API_KEY:-}" || { echo 'NUGET_API_KEY is required.' >&2; exit 1; }
    dotnet nuget push 'artifacts/packages/*.nupkg' --source https://api.nuget.org/v3/index.json --api-key "$NUGET_API_KEY" --skip-duplicate
    if gh release view "$TAG_NAME" > /dev/null 2>&1; then
      gh release upload "$TAG_NAME" artifacts/packages/*.nupkg --clobber
    else
      gh release create "$TAG_NAME" artifacts/packages/*.nupkg --title "$TAG_NAME" --generate-notes --target "$GITHUB_SHA"
    fi
    printf '%s\n' "$TAG_NAME" > artifacts/packages/nuget-published.txt
    gh release upload "$TAG_NAME" artifacts/packages/nuget-published.txt --clobber
    ;;
  *) echo 'Expected resolve or publish' >&2; exit 2 ;;
esac
