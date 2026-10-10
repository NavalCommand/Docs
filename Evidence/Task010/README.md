# Task 010 verification receipts

Read [the implementation report](../../Tasks/010/REPORT.md) for conclusions, scope and limitations. These are dated exports; absolute installation paths here are evidence, not committed build configuration.

- `before-inventory.json`, `before-cmake-targets.json`: actual initial source revisions, layout, outputs and targets.
- `Verify.ps1`: exact VS developer environment, preset configure/build/CTest commands used. Native dependency prefix was reused from the source-built x64-windows-static setup; third-party sources were not rebuilt.
- `<repository>-<debug|release>-{configure,clean,build,ctest}.log`: independent preset/clean/native regression receipts. Build.CMake has no native targets. **gateway.grpc-debug-ctest.log and seapower.mod-debug-ctest.log are FAILED exit-time tests**, not successful tests.
- `direct-project-builds.json`, `direct-*.log`: 14 ordinary direct SDK project rebuilds after configure, no extra property arguments.
- `managed-runtime-tests-detailed.log`, `provider-tests-detailed.log`, `cross-language-detailed.log`, `schema-lock.log`: existing managed, provider, payload and schema regression details.
- `git-source-*.log`, `git-source-receipt.json`, `GitSourceVerification.py`: actual source snapshots, canonical Git URLs via isolated local mirrors, Git FetchContent without Build.Local.cmake and transitive direct managed builds. Public GitHub revisions were not fetched/published.
- `sdkless-*.log`: game-free native configuration/build/CTest pass; requested harness failure is the expected missing SDK diagnostic.
- `package-*.log`, `multi-*.log`, `package-audit.json`: single/multi-config Debug/Release packages, latest Release allowlist and verified manifests.
- `generated-artifact-audit.json`, `tracked-build-path-audit.json`, `source-relocation-hashes.json`, `Audit.py`: no generated outputs outside o/, actual static CRT compile flags, no committed machine-specific build paths, unchanged relocated handwritten code.
- `legacy-relocation.json`: recoverable moves of known ignored outputs after containment/track checks; nothing deleted.
- `mock/`, `live-combined/`, `live-harness-only/`, `live-production-disabled/`: actual current launcher/package regressions. Live runs use forced owned-process cleanup; graceful exit was not tested.
- `LiveVerification.ps1`: narrow acceptance fixture suspends/restores the old developer helper by exact hash, then invokes the current maintained launcher; it is never shipped or imported by maintained sources.
- `installation-before.json`, `installation-after.json`, `integrity-cleanup.json`, `*-legacy-rollback.json`: 235 unchanged installation files, exact rollback and no remaining owned game/deployments.
- `anchorchain-offline-build.log`: optional pinned third-party loader source bootstrap, output under mod o/managed; not installed or redistributed.
- `powershell-parse.json`, `dotnet-info.log`, `vcpkg-revision.txt`, `after-cmake-targets.log`, `implementation-commits.json`: syntax, tooling, targets and separate local commits.

Run fixture scripts from the workspace root. The Audit script excludes only each repository's o/ and Git metadata, verifies actual package files against manifest hashes and checks source moves against the inventory's original Git revision. Generated test mirrors/configurations remain under Build.CMake/o/. Intentional receipts exported here are required task deliverables. Interactive Rider/Visual Studio UI and public remote availability remain unverified.
