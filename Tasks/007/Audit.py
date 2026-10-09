from pathlib import Path
import hashlib
import json
import subprocess
from collections import Counter

WORKSPACE = Path(__file__).resolve().parents[3]
EVIDENCE = WORKSPACE / "Docs/Evidence/Task007"
BASELINE = json.loads((EVIDENCE / "baseline.json").read_text(encoding="utf-8-sig"))
CODE_EXTENSIONS = {
    ".cpp",
    ".h",
    ".cs",
    ".ps1",
    ".py",
    ".cmake",
    ".proto",
    ".csproj",
    ".json",
    ".ini",
}
REPOSITORIES = [
    "Build.CMake",
    "Gateway.Api",
    "Gateway.Grpc.Api",
    "Gateway.Grpc",
    "SeaPower.Client",
    "SeaPower.Mod",
]


def git_output(repository, *arguments):
    return subprocess.check_output(
        ["git", "-C", str(repository), *arguments], text=True
    ).strip()


def category(path):
    if path.name == "CMakeLists.txt" or path.suffix == ".cmake":
        return "CMake"
    return {
        ".cpp": "C++",
        ".h": "C++",
        ".cs": "C#",
        ".ps1": "PowerShell",
        ".py": "Python",
        ".proto": "Proto",
        ".csproj": "Project/config",
        ".json": "Project/config",
        ".ini": "Fixture/config",
    }.get(path.suffix, "Reference/config")


records = []
summary = []
format_checks = []
for name in REPOSITORIES:
    repository = WORKSPACE / name
    baseline = next(entry for entry in BASELINE if entry["repository"] == name)
    original_paths = {entry["path"] for entry in baseline["files"]}
    paths = git_output(
        repository, "ls-files", "--cached", "--others", "--exclude-standard"
    ).splitlines()
    counts = Counter()
    changed_counts = Counter()
    for relative_path in paths:
        if relative_path.startswith(".idea/"):
            continue
        path = repository / relative_path
        if not path.is_file():
            continue
        is_code = path.suffix in CODE_EXTENSIONS or path.name == "CMakeLists.txt"
        if is_code:
            counts[category(path)] += 1
        original = next(
            (entry for entry in baseline["files"] if entry["path"] == relative_path),
            None,
        )
        digest = hashlib.sha256(path.read_bytes()).hexdigest().upper()
        changed = original is None or digest != original["sha256"]
        if changed and is_code:
            changed_counts[category(path)] += 1
        records.append(
            {
                "repository": name,
                "path": relative_path,
                "category": category(path),
                "handwrittenCode": is_code,
                "new": relative_path not in original_paths,
                "changedFromWorkingBaseline": changed,
                "sha256": digest,
            }
        )
        if path.suffix in {".cpp", ".h"}:
            result = subprocess.run(
                ["clang-format", "--dry-run", "--Werror", str(path)],
                capture_output=True,
                text=True,
            )
            format_checks.append(
                {
                    "path": f"{name}/{relative_path}",
                    "exitCode": result.returncode,
                    "output": result.stderr,
                }
            )
    summary.append(
        {
            "repository": name,
            "baseline": baseline["head"],
            "head": git_output(repository, "rev-parse", "HEAD"),
            "filesByCategory": dict(counts),
            "changedByCategory": dict(changed_counts),
        }
    )

(EVIDENCE / "source-inventory.json").write_text(
    json.dumps(records, indent=2) + "\n", encoding="utf-8"
)
(EVIDENCE / "audit-summary.json").write_text(
    json.dumps(summary, indent=2) + "\n", encoding="utf-8"
)
(EVIDENCE / "clang-format-check.json").write_text(
    json.dumps(format_checks, indent=2) + "\n", encoding="utf-8"
)
print(json.dumps(summary, indent=2))
if any(check["exitCode"] for check in format_checks):
    raise SystemExit("C++ formatting check failed")
