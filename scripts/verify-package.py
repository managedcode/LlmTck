#!/usr/bin/env python3
"""Run a real NuGet consumer outside the repository, using an isolated package cache."""
import os
import base64
import hashlib
import json
from pathlib import Path
import subprocess
import tempfile
import shutil
import sys
import xml.etree.ElementTree as ET

root = Path(__file__).resolve().parent.parent
packages = root / "artifacts/packages"
versions = {n.attrib["Include"]: n.attrib["Version"] for n in ET.parse(root / "Directory.Packages.props").iter("PackageVersion")}
version = ET.parse(root / "Directory.Build.props").findtext(".//Version")
consumer = Path(tempfile.mkdtemp(prefix="llmtck-package-consumer-")).resolve()
print(f"Consumer: {consumer}", flush=True)
project = (root / "scripts/package-consumer/Consumer.csproj.template").read_text()
for key, value in {"__VERSION__": version, "__ASPIRE_VERSION__": versions["Aspire.Hosting.Testing"], "__TUNIT_VERSION__": versions["TUnit"], "__PLAYWRIGHT_VERSION__": versions["Microsoft.Playwright"]}.items():
    project = project.replace(key, value)
(consumer / "Consumer.csproj").write_text(project)
(consumer / "PackageTests.cs").write_text((root / "scripts/package-consumer/PackageTests.cs").read_text())
(consumer / "global.json").write_text((root / "global.json").read_text())
env = dict(os.environ, NUGET_PACKAGES=str(consumer / "packages"))
def run(*args):
    subprocess.run(args, cwd=consumer, env=env, check=True)
run("dotnet", "restore", "Consumer.csproj", "--source", str(packages), "--source", "https://api.nuget.org/v3/index.json")
metadata = json.loads((consumer / "packages/managedcode.llmtck.aspire" / version / ".nupkg.metadata").read_text())
expected_hash = base64.b64encode(hashlib.sha512((packages / f"ManagedCode.LlmTck.Aspire.{version}.nupkg").read_bytes()).digest()).decode()
if metadata.get("contentHash") != expected_hash:
    raise RuntimeError("Restored Aspire package differs from the local artifact being verified.")
print("Verified restored package SHA-512 matches the local nupkg.", flush=True)
run("dotnet", "build", "Consumer.csproj", "--configuration", "Release", "--no-restore")
run("pwsh", "bin/Release/net10.0/playwright.ps1", "install", "chromium", *(["--with-deps"] if sys.platform.startswith("linux") else []))
try:
    run("dotnet", "test", "Consumer.csproj", "--configuration", "Release", "--no-build")
finally:
    evidence = root / "artifacts/package-consumer"
    evidence.mkdir(parents=True, exist_ok=True)
    for image in (consumer / "bin").rglob("package-dashboard*.png"):
        shutil.copy2(image, evidence / image.name)
    if (consumer / "TestResults").exists():
        shutil.copytree(consumer / "TestResults", evidence / "TestResults", dirs_exist_ok=True)
