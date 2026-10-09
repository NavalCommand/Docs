# Task 007 implementation report

Date: 2026-10-10 (Berlin; runtime receipts use UTC).

The repository-wide readability refactor and documentation adoption are delivered. Release builds, contract checks, mock hosting, embedded hosting and live regressions passed. **Debug runtime acceptance remains blocked:** gateway and host test processes crash during gRPC cleanup after their assertions pass. The pre-refactor gateway reproduces the crash with the same static dependencies and toolchain. This is not reported as a passing Debug test.

## Documentation authority and scope

Read the current `README.md`, `AGENTS.md`, `ARCHITECTURE.md`, `MODULE_STRUCTURE.md`, `LOCAL_SOURCES.md`, `CODE_STYLE.md` and `CodeStyle.xml` in the Docs Git repository before editing. Each of the six code-repository READMEs now explicitly links to those permanent documents and the `Tasks/README.md` and `Evidence/README.md` archive indexes. Undated build instructions refer to current module policy rather than treating Task 005's historical dynamic-CRT flags as current guidance.

Durable root changes are limited to correcting the README's contradictory AGENTS-copy instruction and explaining the representable formatter settings in CODE_STYLE. The task index adds Task 007. Architecture, module policy, local-source rules, AGENTS and the owner's XML remain unchanged. No AGENTS was created in the unsynchronized parent. Earlier tasks/evidence were hash-checked and preserved; see [archive integrity](../../Evidence/Task007/archive-integrity.json).

No gameplay, AirOps, transport, protocol or Task 006 feature was added. Existing safety checks, selected-side observations, dispatch revalidation, bounded queues, thread ownership, native automation protection and process-lifetime native-module policy remain in place.

## Completed audit

The audit enumerated tracked files rather than traversing generated build/dependency directories. It covered production code, contracts, tests, mocks, CLI, managed integration, diagnostic harnesses, build/launch scripts, schemas, manifests, fixtures, ignore files and READMEs. [Source inventory](../../Evidence/Task007/source-inventory.json) lists each inspected path, category and final working-file hash; [baseline](../../Evidence/Task007/baseline.json) records original revisions, status and hashes.

There were 95 existing handwritten source/configuration files and 114 after splitting existing types into individual files and adding numerical tests. The table counts files, not bugs or methods. “Changed” compares against the initial working files, so already-present static-triplet edits are separately accounted for in the commits below.

| Repository | Inspected source/configuration files after refactor | Changed/new files by category | Readability corrections |
|---|---:|---|---|
| Build.CMake | 6 | CMake 3, PowerShell 1, Python 1 | Expanded resolver fixtures and helper scripts; `c`, `h`, `n`, `local` become canonical identity/hash/fetch-name/local-checkout roles. |
| Gateway.Api | 35 | C++ 19, CMake 4, Proto 4, PowerShell 2, C# 1, project/config 2 | Expanded codecs and tests; `v`/`w`/`u` become domain/wire/entity roles; separated existing domain types into namespace-mirrored headers with compatibility umbrella includes. |
| Gateway.Grpc.Api | 9 | C++ 1, CMake 4, Proto 1, PowerShell 1 | Expanded the complete CLI, including rejection/reconnect/reload branches; named RPC/poll timeouts, poll bounds and waypoint offsets. |
| Gateway.Grpc | 13 | C++ 5, CMake 5, PowerShell 1 | Clarified provider/server/mock roles, private fields, port parsing and shutdown limits; expanded mock/test setup and assertions; aligned server definitions with declaration order. |
| SeaPower.Client | 18 | C# 9, CMake 3, PowerShell 2, project/config 2 | Expanded provider/native facade/tests; named ownership, session, handle, ledger and movement state; named physical conversions and bounds; separated sample types and test facade; explained distance arithmetic. |
| SeaPower.Mod | 33 | C# 7, C++ 5, CMake 4, PowerShell 8, project/config 2 | Separated existing Runtime/Bridge/HostState types; expanded worker and harness logic; named mailbox/result/native-library roles, limits and cadence; expanded argument arrays and reversible launch/cleanup scripts. |

Overall, 97 files were changed or added: C++ 30, C# 17, CMake 23, PowerShell 15, Python 1, Proto 5, project/configuration 6. Four scenario fixtures and other already-readable configurations were inspected and retained. Examples include `Main` → `AssertEngineThread` in the provider/facade, `objects` → `unitsByKey`, `movement` → `pendingMovement`, native `fd`/`vt` → `flightDeck`/`vehicleType`, and `size` → `length` at the host buffer boundary. Serialized labels, native game member names and stable target names were retained.

C++ uses tabs, a 180-column margin, required control-flow braces, sorted angle-bracket includes, uniform member initializers, PascalCase owned functions/types and `m_PascalCase` private fields. The four native `.clang-format` files implement the representable subset; LLVM supplies unspecified formatting defaults. Naming, type/file structure and definition ordering still require review. C# `.editorconfig` files use ordinary C# conventions, without importing C++ naming rules. Formatting tools were applied only to enumerated handwritten files. All 30 final C++ files pass clang-format's dry-run/error check; PowerShell parsing and Python formatting checks also pass.

### Remaining findings and deliberate exceptions

No remaining compressed multi-statement method or opaque internal calculation was identified in the completed source audit. The following compatibility/style exceptions are explicit:

- `Gateway.Api/public/NavalCommand/{World,Orders}/Api/*.h`: existing public aggregate member names remain camelCase to preserve source consumers and aggregate member order. Existing `Order.h` and `View.h` includes remain usable.
- `SeaPower.Client/src/NavalCommand/SeaPower/Client/Provider.cs`: public names `game`, `observer`, `u`, `la`, `lo`, `lb`, `lob` remain for named-argument compatibility. Internal roles/intermediates have descriptive names. The new tests compile named-argument constructor/distance calls.
- `SeaPower.Mod/app/harness/AirOpsProbe.cs`: Harmony's `ltp`, `__instance` and `__result` are binding names; the implementation aliases `ltp` to `launchParameters`. Native game fields retain their actual spelling.
- `SeaPower.Mod/src/NavalCommand/SeaPower/Mod/Host.h` and `Host.cpp`: `nc_*` C exports remain unchanged. Generated protobuf/gRPC names, language entry points, gtest macros and CMake built-ins retain required conventions.
- Small nested private state/delegate types stay beside their owning managed implementation; top-level domain/host/sample types were separated. Single-expression C# accessors are retained where they perform one clear operation.
- The owner's pre-existing TODOs in `SeaPower.Mod/README.md` and `src/CMakeLists.txt`, and untracked `.idea/`, remain uncommitted and preserved. They do not authorize target renaming or package-layout changes in this task.

Generated bindings/output, vcpkg/NuGet dependencies, proprietary assemblies, decompiled sources, binary assets and historical evidence were excluded from source refactoring.

## Numerical and contract preservation

`Provider.Distance(double la, double lo, double lb, double lob)` now names degree/radian values, latitude/longitude Haversine terms and the original 6,371,000-metre radius. Multiplication/division order, repeated sine calls, final radius multiplication and original near-antipodal handling are preserved. No Math.Clamp/Math.Pow substitution or newer game-runtime API was introduced.

Seven regression cases cover coincidence, one equatorial degree, a typical waypoint, the date line, both near-pole cases and a near-antipodal case, with finite/nonnegative/symmetry checks. Every measured result is **bit-identical** to the pre-refactor provider binary: [before](../../Evidence/Task007/distance-baseline.jsonl), [after](../../Evidence/Task007/distance-after.jsonl), [test log](../../Evidence/Task007/provider-final.log).

All five handwritten proto files were compiled before/after into descriptor sets without source-location metadata. They are byte-identical, including message fields, enums, numbers and RPC definitions: [schema compatibility](../../Evidence/Task007/schema-compatibility.json). C# → C++ → C# serialization, optional-zero/unknown-enum coverage, schema-lock validation and the ABI revision-1 tests passed. No public wire schema, C ABI export or CMake target was renamed.

## Executed verification

| Check | Actual result/evidence |
|---|---|
| Baseline CTest in all six repositories | Passed before refactoring; `*-baseline-tests.log`. |
| Fresh configure and clean-first independent Release build in all five functional modules | Passed; `*-configure.log`, `*-build.log`, `*-tests.log`. Final touched gateway/host/provider checks also passed in `Gateway.Grpc-final-*`, `package.log`, `provider-final.log`. |
| Build.CMake dependency-resolution suite | Passed; Git FetchContent without Build.Local, direct/transitive local substitution, invalid/conflicting/late registrations, tag conflicts, canonical identities, shortened names and CMP0168 NEW. [CTest log](../../Evidence/Task007/Build.CMake-tests.log). Git uses isolated local fixtures because repositories remain unpublished. |
| Managed integration/harness build and package composition | Passed, zero build errors; existing Unity assembly-version/deprecation warnings remain recorded in [package log](../../Evidence/Task007/package.log). |
| Cross-language conformance/schema lock | Passed; [conformance log](../../Evidence/Task007/conformance-final.log). |
| Independent generic mock hosting | Passed real gRPC rejection, movement and reconnect/idempotency checks; [mock log](../../Evidence/Task007/mock/cli.log). |
| Embedded live host and waypoint round trip | Passed twice. Final package observed native task creation on engine thread 1, 5.1986616663260605 m displacement, four rejection cases and retained result after reconnect. [Final CLI](../../Evidence/Task007/live-waypoint-final/cli.log), [dispatch log](../../Evidence/Task007/live-waypoint-final/gateway.log). Movement is not arrival. |
| Live protected native action | Passed: advertised protection and rejected at dispatch as `protected_native_action`; [CLI](../../Evidence/Task007/live-protected/cli.log). |
| Live unload/reload | Passed new session/handles and old-session/old-handle rejection; [CLI](../../Evidence/Task007/live-reload/cli.log). |
| Debug clean builds in all five functional modules | Compilation/linking passed. Gateway.Api, Gateway.Grpc.Api and provider CTest passed. Gateway.Grpc and host-ABI CTest **failed** at process teardown; assertions alone were green. Final incremental Debug rebuilds repeat the failures in `*-final-debug-tests.log`. |
| C++ formatting, PowerShell syntax, Python formatting, Git whitespace | Passed; `clang-format-check.json`, `powershell-parse.json`, `python-format.log`; final repository diffs were checked. |

Early verification caught an accidental protoc plugin-option rename, generated Unit accessor rename and missed PowerShell splat rename; these were corrected before final successful checks. They did not introduce feature changes.

Whitespace checks apply to handwritten source and documentation. Raw command logs and original patch snapshots retain their recorded whitespace, including Git's evidence-file whitespace warnings.

Live runs used only the previously approved opt-in reversible harness, disposable existing fixtures and owned processes. All 233 recorded vendor files match their original hashes. Plugin and Doorstop configuration were restored byte-for-byte, and no game process remains: [installation after](../../Evidence/Task007/installation-after.json), [vendor after](../../Evidence/Task007/vendor-after.json). Existing BepInEx/Unity logging warnings were observed; the readiness and end-to-end receipts nevertheless passed. AirOps launch experiments, Linux builds, normal-user graceful game shutdown and Debug in-game deployment were not executed.

## Static CRT adoption and Debug blocker

The five active Windows preset pairs and dependency preparation use `x64-windows-static` for host and target. Static-runtime changes were isolated in the commits below. Fresh configuration and clean module relinking reused the already provisioned, source-built static vcpkg prefix; another third-party rebuild was unnecessary. The provisioning check confirms all requested packages are installed; historical Task 005 flags/evidence were not rewritten.

Toolchains: CMake 4.2.2, Ninja, VS 2026 Insiders MSVC 19.51.36260/toolset 14.51.36231 for project modules; vcpkg selected VS 2022 MSVC 14.44.35207 for external packages. Pinned vcpkg revision is `3ddaad9be959816602453ecb05533f8732464ef4`. Dependencies are protobuf 33.4, gRPC 1.81.1 and the versions recorded in [dependency status](../../Evidence/Task007/dependency-status.txt); .NET SDK is 10.0.401.

Every module compile-command set uses `/std:c++latest` and exclusively `/MT` in Release or `/MTd` in Debug: [runtime flags](../../Evidence/Task007/runtime-flags.json). gRPC library directives confirm LIBCMT/LIBCMTD; the Release host DLL imports only Windows system DLLs, with no dynamic MSVC CRT or separate gRPC/protobuf DLL: [import inspection](../../Evidence/Task007/host-release-dependents.log). Dependency names remain deterministic 15-character `nc_<12 hex>` names. No committed Build.Local, developer preset, workspace-root environment resolver or machine-specific source substitution was introduced.

**Outstanding Debug runtime blocker:** `Gateway.Grpc` and `SeaPower.Mod` processes terminate with access violation after their gtests pass. The original Gateway.Grpc source at `5e80a356...`, built in a temporary checkout with the same current static dependencies/Debug preset, fails identically: [baseline comparison](../../Evidence/Task007/Gateway.Grpc-baseline-debug-tests.log). The debugger shows `_Mtxlock`/`std::_Container_base12::_Orphan_all_locked_v3` while a gRPC WindowsEventEngine worker destroys its timer containers: [stack](../../Evidence/Task007/gateway-debug-exit-stack.log). The underlying cause is unresolved; the mixed compiler versions are evidence, not a proven cause. Release static-CRT deployment is verified; a complete Debug runtime-compatibility claim is blocked.

Reproduction: run the Debug preset and CTest, or the helper below with `-ContinueAfterTestFailure`. Next investigation should first rebuild third-party Debug dependencies and consumers with one matching toolset, then compare the gRPC shutdown stack and lifetime behavior. Do not mask this with `_Exit`, skip failing tests, change gameplay-thread shutdown ownership or silently restore dynamic CRT.

## Reproduction and review commits

[Verify.ps1](Verify.ps1) performs fresh independent configurations, clean builds and tests using caller-supplied toolchain/dependency paths. Enter a developer shell with Ninja available, preserve ignored Build.Local registrations and run from the parent workspace:

```powershell
./Docs/Tasks/007/Verify.ps1 -CMake $cmake -VsDevCmd $vsDevCmd -VcpkgRoot $vcpkgRoot -DependencyPrefix $staticPrefix
./SeaPower.Mod/scripts/Build.ps1 -GameRoot $gameRoot -CMake $cmake -BuildCMakeSource "$PWD/Build.CMake"
./SeaPower.Mod/scripts/Test-Mock.ps1 -HostExecutable "$PWD/Gateway.Grpc/o/windows-msvc-release/app/mock-host.exe" -Cli "$PWD/Gateway.Grpc.Api/o/windows-msvc-release/app/naval-cli.exe" -OutputDirectory "$PWD/Docs/Evidence/Task007/new-mock"
./SeaPower.Mod/scripts/Start-Live.ps1 -GameRoot $gameRoot -Cli "$PWD/Gateway.Grpc.Api/o/windows-msvc-release/app/naval-cli.exe" -OutputDirectory "$PWD/Docs/Evidence/Task007/new-live" -Smoke
python ./Docs/Tasks/007/Audit.py
```

Use fresh live output directories. `-Blocked` additionally supplies the existing `AircraftThreat.ini`; `-ReloadTest` uses the default fixture. The package remains `SeaPower.Mod/out/package/0.1.0`; build directories remain `o/<preset>`. [Audit.py](Audit.py) refreshes the inventory and clang-format checks, not the source. It requires clang-format 23; formatting tools used also included Black 26.10, cmakelang 0.6.13, PSScriptAnalyzer and SDK Roslyn tooling. No tool installation was committed as a runtime dependency.

All repositories remain local; nothing was pushed. Full hashes and initial/pre-report status are in [commits](../../Evidence/Task007/commits.json).

| Repository | Initial revision | Static-policy commit | Mechanical commit | Readability/final source commits |
|---|---|---|---|---|
| Build.CMake | 3d3ff24 | 4b00695 (documentation) | 03a330c | ae4b4ef |
| Gateway.Api | 1578f7f | 17c7d4d | accbb3e | ca5b9d8 |
| Gateway.Grpc.Api | 267b9f4 | 97637c8 | ea36217 | 71cf5c6 |
| Gateway.Grpc | 5e80a35 | 827faa9 | ed9b77c | 476c9bc |
| SeaPower.Client | 284f287 | b089ec4 | 1a14df1 | c24afed (ownership/names), f6055f7 (distance/tests) |
| SeaPower.Mod | eb31e6e | 7b6d424 | 472fb5f | 861d455 (host/composition), 9800829 (harness/scripts) |
| Docs | f7733a7 | Existing normative policy retained | Owner XML unchanged | fb688d4 (durable authority/style corrections); this report/evidence are in the subsequent task-artifact commit |

Mechanical and identifier changes are separately reviewable; the significant distance refactor/tests have their own commit. Build and all functional repos are clean except the preserved owner TODOs and `.idea/` in SeaPower.Mod. The Docs pre-report snapshot contains only the new Task 007 artifacts; the final artifact commit files them under `Tasks/007/` and `Evidence/Task007/`. Permanent documents contain no execution journal, and archived/proprietary assets remain unchanged.
