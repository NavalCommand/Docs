# Task 010 — Unified managed build architecture

Implemented on 2026-10-10 against the actual current local source checkouts. Build/layout verification passed with **two known Debug native process-exit failures retained**. This is not a gateway teardown fix. No pushes were made.

## Baseline and scope

The permanent AGENTS, ARCHITECTURE, DEVELOPMENT, MODULE_STRUCTURE, CODE_STYLE, MANAGED_BUILD and LOCAL_SOURCES documents were read first; the owner's CodeStyle.xml was retained verbatim. The separate Docs repository remains authoritative. There is no parent AGENTS.md. See [before-inventory.json](../../Evidence/Task010/before-inventory.json) and [before-cmake-targets.json](../../Evidence/Task010/before-cmake-targets.json) for source revisions, tracked layout, existing ignored output trees and actual CMake declarations. SeaPower.Mod's source baseline was `4fed009aeb78d9ae3fb0bf215fc8856603831bbf`.

Only build infrastructure, source ownership, output paths, tests/orchestration and corresponding documentation changed. No World.Api/Orders.Api relocation, new domain repositories, Task 006/008 implementation, gameplay investigation, protocol/schema/RPC/ABI/order change, runtime logic change, harness activation change or gateway rewrite occurred. Gateway.Grpc and Gateway.Grpc.Api source repositories remain unchanged. The existing host-rename TODO remains out of scope; SeaPower.Host retains its name and ABI.

## Before → after

| Owner | Before | Implemented location/behavior |
|---|---|---|
| SeaPower.Client project | root `SeaPower.Client.csproj` | `managed/SeaPower.Client.csproj` |
| Provider C# | `src/NavalCommand/SeaPower/Client/*.cs` | `managed/src/NavalCommand/SeaPower/Client/*.cs`; namespaces/assembly unchanged |
| Provider fake tests | `test/*.cs`, `test/ProviderTests.csproj` | `test/managed/`; updated source links |
| Provider CMake | native-style `src/CMakeLists.txt`, Release-only dotnet invocation | `managed/CMakeLists.txt`, explicit `SeaPower.Client.Managed`, configured MSBuild context |
| Gateway.Api managed outputs | `managed/bin`, `managed/obj`, generated C# beneath source-adjacent obj | `o/managed/Gateway.Api/{bin,obj}/`; generated canonical bindings remain intermediate files |
| API conformance outputs | `test/managed/bin,obj` | `o/managed/Conformance/{bin,obj}/`; existing cross-language checks integrated into CTest |
| Mod composition discovery | `app/CMakeLists.txt` → mandatory `managed-paths.json` PowerShell phase | `managed/CMakeLists.txt` and generated MSBuild imports; old JSON phase removed |
| Runtime/harness/tests | source-adjacent `managed/.../bin,obj`, `test/managed/bin,obj` | separate `o/managed/SeaPower.Mod`, `SeaPower.Harness`, `RuntimeTests` projects |
| Distribution stages | `out/package/0.1.0`, `out/harness/0.1.0` | `o/package/0.1.0`, `o/harness/0.1.0` |
| PowerShell full builds | repeated dependency properties, independent native/dotnet build graph, hardcoded Release | thin calls to configured CMake package/module targets |
| Shared infrastructure | native module discovery only | native plus managed discovery, context/project registration, MSBuild output metadata and regression fixtures |

Nine relocated handwritten C# files have identical canonical Git content after CRLF normalization; [source-relocation-hashes.json](../../Evidence/Task010/source-relocation-hashes.json) records the checks. All maintained C# is now under managed/ or test/managed/ in the six audited source/infrastructure repositories. Experiments and frozen Docs archives were not relocated or rewritten.

## One resolver, ordinary managed projects

`NavalCommandAddDependency()` / FetchContent and ignored Build.Local.cmake remain the only internal source resolver. Existing canonical identities, transitive reuse, conflict checks, CMP0168 NEW and deterministic 15-character `nc_<hash>` names remain intact.

Build.CMake adds `NavalCommandManagedContext(Name=value ...)`, `NavalCommandManagedProtoc(output)` and `NavalCommandAddManagedProject(target project.csproj)`. Each managed/CMakeLists describes its own projects and resolved requirements. No configure-time dotnet build/restore, parallel NuGet resolver, custom C# compiler, solution generator or emulated ProjectReference graph was introduced.

The three managed repositories commit early Directory.Build.props output rules, using their **own** MSBuildThisFileDirectory and MSBuildProjectName. They optionally import `o/managed/Build.Context.props`, generated from actual resolved checkouts, pinned host protoc, shared Managed.targets and configured game-reference paths. No developer-specific path was committed to build configuration. The mod preset's existing concrete vcpkg location became the standard VCPKG_ROOT toolchain expression while retaining the selected static triplets/runtime.

After configuration, normal dotnet/MSBuild projects use real ProjectReferences:

```text
SeaPower.Mod → SeaPower.Client → Gateway.Api → standard Google.Protobuf NuGet package
Conformance → Gateway.Api
ProviderTests → Gateway.Api + unchanged engine-independent provider source links
SeaPower.Harness → compile-only approved game/BepInEx references (no mod/provider/API)
RuntimeTests → unchanged owned runtime/harness test seams (no game references)
```

Fake-provider source links deliberately remain: referencing the full production project would require proprietary game assemblies and compromise the existing game-free tests. MSBuild owns transitive restore/build and target-framework checks. Library targets are explicitly requested rather than separately scheduled alongside a consuming runtime graph. A Ninja managed job pool serializes managed targets within a CMake build tree; independent build trees sharing managed projects must not be built concurrently.

### Active context and stale-consumer safety

The active IDE context is configuration-independent. Identical source/tool/SDK paths are reused without rewriting the file, so direct Debug/Release changes and C# edits need no CMake rerun. Changed dependency identities/checkouts, shared tool checkout/protoc location or game SDK require configuration.

Conflicting contexts are rejected. `-DNAVALCOMMAND_REPLACE_MANAGED_CONTEXT=ON` explicitly selects a replacement after closing other builds, and resets OFF after the successful top-level configure. Each CMake managed target captures its configured dependency contexts and checks them before MSBuild. An older consumer cannot silently compile against another consumer's context. Direct IDE/MSBuild uses the selected context; reconfigure other consumers, or use separate source checkouts for incompatible concurrent contexts. Git-fetched Build.CMake/tool/source directories referenced by the context must remain present.

The generic CTest fixture verified local and Git direct/transitive contexts, real Debug/Release ProjectReference builds, conflict rejection, explicit replacement, stale consumer rejection and permission reset. The actual updated mod source graph was also configured through Git FetchContent with no Build.Local.cmake, then built directly in Debug/Release.

## Build and packaging workflows

The permanent [MANAGED_BUILD.md](../../MANAGED_BUILD.md) documents configure, repeated IDE/MSBuild and full CMake package workflows separately. From a VS 2026 x64 developer shell with CMake 4.2+, Ninja, .NET SDK, PowerShell 7 and standard VCPKG_ROOT:

```powershell
# From SeaPower.Mod; local source/SDK overrides belong in ignored Build.Local.cmake.
cmake --preset windows-msvc-release -DFETCHCONTENT_SOURCE_DIR_NCBUILD=/absolute/Build.CMake
# Repeat these after configuration without extra -p inputs or rerunning CMake:
dotnet build managed/runtime/SeaPower.Mod.csproj -c Release
dotnet build managed/harness/SeaPower.Harness.csproj -c Debug
# Full distributions:
cmake --build o/windows-msvc-release --target package-runtime
cmake --build o/windows-msvc-release --target package-harness
ctest --test-dir o/windows-msvc-release --output-on-failure --no-tests=error
```

`package-runtime` orders native host → top-level runtime MSBuild graph → packaging. `package-harness` compiles only the independent harness, then stages its DLL/scenarios. CMake-selected `$<CONFIG>` reaches dotnet and CTest; both Ninja single-config and Ninja Multi-Config Debug/Release targets were executed. The wrappers no longer configure or choose a second build graph. Single-config build type is selected at configuration; wrapper `-Configuration` is for multi-config selection. No extra preset names were introduced.

MSBuild records evaluated TargetPath and copy-local paths after successful compilation. Packaging selects the four permitted managed DLLs using those records; it does not guess bin/Release/framework paths or manually locate a NuGet DLL. SDK outputs retain configuration/platform/framework dimensions, including the x64 directory supplied by the developer environment. Current projects each have one framework; versioned package staging is intentionally one selected configuration at a time, not a concurrently writable Debug/Release stage.

Fresh stages preserve existing stages as uniquely named recoverable backups under o/. Production allowlist, disabled configuration, native host ABI, manifests/checksums and exclusion of harness/scenarios/probes/game assemblies/System/Unity shims remain intact. The restored Google.Protobuf package has no separate LICENSE file; the resolved Protobuf redistribution license remains packaged, as in the previous implementation. No loader is installed/activated by a build.

Build-AnchorChain.ps1 remains a narrowly scoped optional third-party loader source-bootstrap utility, outside the NavalCommand dependency graph. Its explicitly selected source work directory must be inside this mod's o/; the generated project inherits the owning repository's output policy and returns evaluated MSBuild TargetPath. Its pinned source/hash build passed, writing `o/managed/AnchorChain`; nothing from this check was installed or added to either distribution.

## Executed verification

Receipts and exact environment/commands are indexed in [Evidence/Task010/README.md](../../Evidence/Task010/README.md).

| Check | Result |
|---|---|
| Clean independent native builds of five functional repositories, Debug and Release | **Passed** using actual presets, clean target and source-built static dependency prefix |
| Build.CMake Debug and Release configure/build/CTest | **Passed**, both resolver and managed integration suites |
| Seven normal managed projects rebuilt directly in both configurations | **Passed: 14 rebuilds**, no manual dependency/protoc/game properties or CMake rerun |
| Runtime transitive ProjectReference outputs and canonical Protobuf generation | **Passed**, each owning repository's o/managed directories |
| API codec, cross-language conformance, imported service checks, provider tests | **Passed** in Debug and Release; unknown fields, optional zero, unknown enums and schema lock retained |
| Runtime/harness managed tests, including native negative fixtures | **Passed: 27 checks**, also passed via configured Debug/Release CTest |
| Release gateway and host ABI CTest | **Passed** |
| Clean Debug Gateway.Grpc CTest | **FAILED: segmentation fault after all 3 assertions passed**, existing unresolved teardown issue |
| Clean Debug SeaPower.Mod host-abi CTest | **FAILED: segmentation fault after its assertion passed**, same retained exit-failure status; managed CTest passed |
| Direct/transitive Build.Local overrides and Git/no-local resolver tests | **Passed**, including invalid/conflicting/late registration rejection and short dependency names |
| Actual updated source graph via ordinary Git FetchContent | **Passed** against isolated local Git mirrors of canonical URLs; no Build.Local.cmake |
| Native-only mod configure/build/CTest without game SDK | **Passed**, 2/2 CTest; requested game-dependent harness **correctly rejected** with explicit SDK diagnostic |
| CMake package-runtime/package-harness, Debug and Release, Ninja and Ninja Multi-Config | **Passed: 8 package target executions**, evaluated configuration-specific outputs and separate distributions |
| Package allowlist, exclusions, fresh-stage and recursive manifest hashes | **Passed**; latest Release production has 21 files including manifest; harness has only own DLL/scenarios/manifest |
| Independent mock host/client smoke | **Passed**, 111 m mock movement |
| Live Release combined waypoint/rejection regression using CMake-staged package | **Passed**, 5.16173 m native movement and four invalid requests rejected |
| Live harness-only run with production plugin absent | **Passed**, token/PID playable readiness; loader confirms no production mod loaded |
| Live production-only disabled posture with harness absent | **Passed**, production plugin independently bootstrapped and logged disabled; no gateway started |
| Generated artifact and handwritten C# ownership scan | **Passed**, zero build outputs outside o/ in the six source repositories |
| Native runtime flags/toolchain | **Passed**, static x64-windows-static dependency prefix, -MT/-MTd and /std:c++latest in actual compile commands |
| Game/vendor/loader/legacy-helper integrity and cleanup | **Passed**, all 235 checked files unchanged, zero owned deployments/game processes, exact disabled Doorstop restored |
| Interactive Visual Studio/Rider UI/design-time build behavior | **Not executed**; actual ordinary dotnet/MSBuild direct builds passed |
| Public GitHub availability of unpublished source revisions | **Not tested**; isolated local Git mirrors were used and nothing pushed |
| Enabled production-only live mission, protected-aircraft/reload and Debug live-game runs | **Not rerun** in this layout task; maintained code unchanged, combined/harness/disabled and mock checks executed as listed |

The dependency prefix was already source-built for the prescribed static CRT/triplet; third-party libraries were **not** rebuilt in this task. No claim of a clean third-party rebuild is made. Tools were VS 2026 Insiders MSVC 19.51.36260, CMake 4.2.2 and .NET SDK 10.0.401. Compiler/tool receipts retain actual paths and flags.

The two Debug failures are **not passed tests**, despite successful assertions. Their process exit is failed, captured and not suppressed, skipped or rewritten. The task's layout/integration changes are implemented; an entirely green Debug native regression gate remains blocked by this preexisting issue. Gateway code was left untouched. The existing game-reference assembly-version warnings and BepInEx Unity log-writer warning remain visible; no suppression or unrelated loader/gateway repair was attempted.

Live fixtures suspended/restored only the old developer plugin, used the current maintained launcher and explicit approved temporary loader activation, and preserved owned-process cleanup. The three live runs used forced cleanup, **not** proof of graceful shutdown. All original game DLLs/executables and loader/helper bytes were hashed before/after. No new gameplay capability was added.

## Legacy outputs, cleanup and remaining limits

Existing ignored source-adjacent bin/obj/out trees were inventoried, checked for repository containment and absence of tracked files, then **moved, not deleted**, into each owning `o/legacy-generated/`. [legacy-relocation.json](../../Evidence/Task010/legacy-relocation.json) records every source/destination. Prior cached source trees were preserved inside those backups. No arbitrary user directory or historical Docs evidence was removed.

For future disk reclamation: review the exact recorded backup destinations, verify they still resolve under the matching repository's o/, preserve any needed old distribution, and remove only those approved known generated backup trees. Versioned package `.previous-*` stages can be reviewed similarly. This optional cleanup was not silently performed. External NuGet caches, user-selected run evidence and the game installation remain outside repository build ownership; the requested Docs evidence exports are deliberate, not source-adjacent build products.

Permanent AGENTS, ARCHITECTURE, DEVELOPMENT, MODULE_STRUCTURE, MANAGED_BUILD/index and affected module READMEs were updated. CODE_STYLE.md and CodeStyle.xml were read and preserved; archived reports were not rewritten. Handwritten staged files passed whitespace checks; raw tool logs intentionally retain their original trailing whitespace and blank lines. No out-of-scope design problem was repaired. Task 008 remains not started; the known shutdown failure is the explicitly recorded runtime follow-up.

## Local implementation commits

| Repository | Commit | Change |
|---|---|---|
| `Build.CMake` | `477d8055a366` | Integrate managed project contexts and MSBuild orchestration |
| `Gateway.Api` | `bc141744364c` | Keep managed codegen and conformance outputs under o |
| `SeaPower.Client` | `5c7a8862657b` | Move managed provider sources and integrate independent MSBuild builds |
| `SeaPower.Mod` | `f333d1c59115` | Unify native managed builds and separate packaging under o |

The separate Docs commit contains this report, permanent policy updates and verification receipts. No commits were pushed. Source repositories are clean after their separate commits; unaffected gateway repositories remain at their original source revisions.
