# Task 007 evidence

Read with [the implementation report](../../Tasks/007/REPORT.md). These artifacts describe the actual Windows runs, including failed Debug process exits.

- `baseline.json`, `*-before.patch`, `*-baseline-tests.log`: original revisions, uncommitted changes, tracked-file hashes and passing baseline suites.
- `source-inventory.json`, `audit-summary.json`, `audit.log`: complete final handwritten-file inventory and category counts. File counts measure the change footprint, not bug counts.
- `mechanical-inventory.json`: early formatter enumeration. Its mechanicallyChanged flag predates the later C#/PowerShell formatting steps; use the final inventory and Git commits for complete change accounting.
- `*-configure.log`, `*-build.log`, `*-tests.log`: independent clean Release verification. Final incremental checks are labeled `final`; `package.log` includes final host-ABI and managed/harness package builds.
- `*-debug-*`, `*-final-debug-*`: Debug compile/test runs. Gateway and host tests fail at process exit even when gtest prints passing assertions. `Gateway.Grpc-baseline-debug-*` uses the pre-refactor gateway with the same dependency prefix; `gateway-debug-exit-stack.log` records the debugger stack.
- `runtime-flags.json`, `grpc-*-directives.log`, `host-release-dependents.log`: static CRT, C++ compiler mode, shortened dependency names and Release DLL imports.
- `dependency-status.txt`, `dependency-prefix-check.log`: existing compatible static prefix. The prefix check reused installed packages; it is not a fresh third-party rebuild log.
- `schema-before.bin`, `schema-after.bin`, `schema-compatibility.json`: identical descriptor sets compiled from original and final handwritten schemas, without source-location metadata.
- `conformance-final.log`, `csharp.bin`, `cpp.bin`: final cross-language/schema-lock checks. Earlier first-pass logs/payloads are separately retained.
- `distance-baseline.jsonl`, `distance-after.jsonl`, `provider-final.log`: numerical baseline, bit-identical final results and provider regressions.
- `mock/`, `live-waypoint/`, `live-protected/`, `live-reload/`, `live-waypoint-final/`: opt-in run receipts and logs. `live-waypoint-final` exercised the final packaged source; earlier live receipts precede the last arithmetic/declaration-order readability edits.
- `vendor-before.json`, `vendor-after.json`, `installation-before.json`, `installation-after.json`, `archive-integrity.json`: unchanged vendor assets, exact loader/helper rollback and archived-document/XML integrity.
- `clang-format-check.json`, `powershell-parse.json`, `python-format.log`: final native formatting, all 15 tracked PowerShell parsers and Python formatting checks.
- `commits.json`: code-repository final hashes and the Docs status **before** committing this report/evidence. The artifact commit itself is discoverable in the Docs Git log.

All scenario/game identifiers and timestamps are run evidence. They are not permanent API guarantees. Temporary backup paths in cleanup receipts identify exact retained helper backups, not committed build configuration.
