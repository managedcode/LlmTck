#!/usr/bin/env python3
"""Exercise release recovery with local fake gh/NuGet CLIs; never publish externally."""
import os
from pathlib import Path
import subprocess
import tempfile

script = Path(__file__).with_name("release.sh").resolve()
with tempfile.TemporaryDirectory(prefix="llmtck-release-test-") as directory:
    root = Path(directory)
    (root / "artifacts/packages").mkdir(parents=True)
    (root / "artifacts/packages/example.nupkg").write_text("fixture")
    for name, code in {
        "dotnet": '''#!/usr/bin/env bash
set -eu
if [ "$1" = msbuild ]; then echo 0.1.1; exit 0; fi
if [ -e fail-push ]; then touch partial-package; exit 1; fi
touch packages-published
''',
        "gh": '''#!/usr/bin/env bash
set -eu
case "$2" in
 view) test -e release-exists || exit 1; if [ -e delivered ]; then echo nuget-published.txt; fi ;;
 create) test -e packages-published; touch release-exists ;;
 upload) test -e packages-published; case "$4" in *nuget-published.txt) touch delivered;; esac ;;
esac
''',
    }.items():
        file = root / name
        file.write_text(code)
        file.chmod(0o755)
    output = root / "output"
    env = dict(os.environ, PATH=str(root) + os.pathsep + os.environ["PATH"], GITHUB_OUTPUT=str(output),
               NUGET_API_KEY="fake-test-key", TAG_NAME="v0.1.1", GITHUB_SHA="fixture-sha", FORCE_RELEASE="false")
    def run(action, success=True):
        result = subprocess.run(["bash", str(script), action], cwd=root, env=env, capture_output=True, text=True)
        assert (result.returncode == 0) == success, result.stderr
    def should_release(expected):
        output.write_text("")
        run("resolve")
        assert f"should_release={str(expected).lower()}" in output.read_text()
    # An older partial GitHub release must not prevent repairing NuGet delivery.
    (root / "release-exists").touch()
    should_release(True)
    (root / "fail-push").touch()
    run("publish", False)
    assert not (root / "delivered").exists()
    should_release(True)
    (root / "fail-push").unlink()
    run("publish")
    should_release(False)
    env["FORCE_RELEASE"] = "true"
    should_release(True)
    env["NUGET_API_KEY"] = ""
    run("publish", False)
    # New version path: release is created only after NuGet success.
    env["NUGET_API_KEY"] = "fake-test-key"
    (root / "release-exists").unlink()
    (root / "delivered").unlink()
    run("publish")
    assert (root / "delivered").exists()
print("Release recovery: partial publish, rerun, completion, force and missing-key checks passed.")
