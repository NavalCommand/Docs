# Task 011 evidence

Read together with [the implementation report](../../Tasks/011/REPORT.md).

- `before-inventory.json`, `header-hashes.json`, repository reference receipts: baseline, ownership and exact header relocation.
- `Verify.ps1`, `results-Release.json`, `results-Debug.json`, per-repository build/test logs: independent clean native builds, managed graph/tests, direct project builds and packages. Debug gateway and host exit failures are retained.
- `*-details.log`: actual CTest commands and successful/failing test output, including managed test details.
- `GitVerification.py`, `git-resolution.json`, `git-*.log`: isolated local snapshot Git FetchContent with no Build.Local; this is not a public publication check.
- `public-remote.log`: expected Domain.Api GitHub URL unavailable, exit 128.
- `shared-resolver-*.log`: existing Build.CMake source-resolution and managed integration suites, 2/2 passed.
- `mock-*.log`: Release mock-host/CLI round trip, 111 m mock movement; host stopped by its owned PID.
- `Audit.py`, `source-and-output-audit.json`, `package-audit-*.json`: unchanged protected code/schema, exact moved headers, output containment, CRT flags and package manifest/exclusions.
- `FinalReceipt.py`, `final-revisions.json`, `generated-protobuf-hashes.json`, `generated-csharp-hashes.json`, `descriptor-compatibility.json`: source commits, ignored overrides, no tracked workspace paths, matching generated Protobuf output and identical baseline/current descriptor sets.
- `new-source-format.log`, `final-*.log`: formatter and final new-consumer rebuild/retest checks.
- `schema-lock.log`: canonical schema lock and incompatible-field negative regression.

Absolute local paths in these reproduction receipts describe this verification environment. They are not committed module build configuration. No live game, interactive IDE or public-remote build is marked passed.
