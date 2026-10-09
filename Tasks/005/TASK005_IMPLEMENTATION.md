# Module structure and build system — Task 005

Task 005 is a structural refactoring of completed Task 004. Gateway implementation, game facade/provider, canonical payload files, RPC service, C ABI and test bodies are preserved. No gameplay, AirOps, LLM or gateway capability was added. The existing five local Git repositories remain independent; **Build.CMake** is a sixth independent local repository for build infrastructure. No repository was published or pushed.

The reference conventions are [UnrealVoxelSim/ModuleTemplate](https://github.com/UnrealVoxelSim/ModuleTemplate) and [UnrealVoxelSim/Build.CMake](https://github.com/UnrealVoxelSim/Build.CMake). Their source/AGENTS/CMake were inspected locally. NavalCommand adapts the layout, target policy and compact canonical FetchContent identities; it follows this task's explicit Build.Local.cmake registration mechanism instead of the reference project's workspace-root discovery. C++26 takes precedence over the reference template's C++23 default. Windows now uses the static MSVC runtime, following the post-Task-005 triplet update. Existing public target names remain compatible.

## Before and after

[Evidence/Task005/before-layout.json](../../Evidence/Task005/before-layout.json) records every original tracked module path, original revision and root target declaration before edits. Task 004 had production/test/application declarations in native root CMakeLists.txt, four copies each of Common.cmake, Modules.cmake and vendored CPM.cmake, developer-local Git URLs in those files/managed locks, custom compiler/linker/workspace environment variables, and configure/build/test presets using out/native. SeaPower.Client used a separate PowerShell Git resolver.

| Repository | Production | Mocks | Tests | Applications/composition |
|---|---|---|---|---|
| Gateway.Api | src/CMakeLists.txt: Gateway.Api; namespace-mirrored Codec.cpp, canonical generated bindings | — | test/CMakeLists.txt: api-tests/codec; existing managed conformance/schema guard | app/CMakeLists.txt: schema-roundtrip |
| Gateway.Grpc.Api | src/CMakeLists.txt: Gateway.Grpc.Api generated service bindings | — | test/CMakeLists.txt: imports | app/CMakeLists.txt: naval-cli |
| Gateway.Grpc | src/CMakeLists.txt: Gateway.Grpc unchanged Server.cpp | mock/CMakeLists.txt: Gateway.Grpc.Mock INTERFACE for existing MockProvider.h | test/CMakeLists.txt: gateway-tests/gateway | app/CMakeLists.txt: mock-host |
| SeaPower.Client | src/CMakeLists.txt: SeaPower.Client.Managed compile-only integration; original namespace-mirrored C# sources | Existing test fake remains test-local | test/CMakeLists.txt: provider dotnet test | — |
| SeaPower.Mod | src/CMakeLists.txt: SeaPower.Host shared library; private src/NavalCommand/SeaPower/Mod/Host.cpp/.h | — | test/CMakeLists.txt: host-tests/host-abi | app/CMakeLists.txt: resolved managed-source metadata; existing approved harness remains app/harness; scripts/Build.ps1 packages composition |
| Build.CMake | NavalCommand.Build.CMake.cmake central policy/resolver; no runtime library | — | test/ResolutionTest.py actual FetchContent/Git fixture tests | scripts/Build-Native.ps1 shared build/test runner |

Public namespace paths are unchanged. Moved app/test/native files retain identical source bodies. There are no empty benchmark directories or new runtime implementations. The Host header is a private composition ABI, now beside its production implementation. The ABI test's loader path adds the host output directory only during that test; the packaged DLL layout and runtime loader are unchanged.

Root native CMakeLists.txt now performs only project/policy initialization, minimal Build.CMake FetchContent bootstrap, canonical dependency declarations and module composition. Production/mocks/tests/apps are defined in their corresponding subdirectories. Existing target names/output filenames and exported methods are preserved. The only added mock target names the existing reusable header-only mock. SeaPower.Client now has a canonical CMake entry point; its game references remain compile-only and its fake tests still run without the game.

## Canonical dependency resolution

```cmake
NavalCommandAddDependency(
  https://github.com/NavalCommand/Gateway.Api.git
  main)
```

Every ordinary internal dependency uses that API. URL identity is owner.repository, case-normalized and stripped of .git. The FetchContent name is `nc_` plus the first 12 hex characters of SHA-256 of that full identity: 15 characters regardless of module name length. Example Gateway.Api → `nc_0b1845c5e8eb`, Gateway.Grpc.Api → `nc_2ce4f16e7409`. CMP0168 NEW avoids nested FetchContent subbuild trees. Shortening changes generated dependency directory names, not public targets/namespaces.

Registry state is global to the configure graph. Repeated identities resolve once; conflicting Git tags or local registrations fail. Paths must be valid directories containing CMakeLists.txt, normalized with REAL_PATH. Late override changes fail. Registered dependencies declare SOURCE_DIR before MakeAvailable; unregistered dependencies declare the exact canonical Git URL/tag. No production Git rewriting, workspace-root environment variable, implicit sibling lookup or second managed dependency map exists. CMake-generated managed-paths.json supplies Mod's C# build with the same resolved sources/protoc used by the native graph.

Build.CMake automatically loads **only** `${CMAKE_SOURCE_DIR}/Build.Local.cmake`, once before any ordinary dependency is declared. Its registrations apply to direct and transitive dependencies. It never reads developer files inside fetched modules. Build.Local.cmake, o/ and CMakeUserPresets.json are Git-ignored in every repository. Explicit path mistakes do not silently fall back to network fetching.

The actual developer files in this checkout register the canonical URLs against each local directory under `C:/Software/GitHub/NavalCommand`. This is intentionally untracked. For another developer, an example ignored file is:

```cmake
NavalCommandSetDependencyPath(
  https://github.com/NavalCommand/Gateway.Api.git
  "D:/Projects/NavalCommand/Gateway.Api")
NavalCommandSetDependencyPath(
  https://github.com/NavalCommand/Gateway.Grpc.Api.git
  "D:/Projects/NavalCommand/Gateway.Grpc.Api")
NavalCommandSetDependencyPath(
  https://github.com/NavalCommand/Gateway.Grpc.git
  "D:/Projects/NavalCommand/Gateway.Grpc")
NavalCommandSetDependencyPath(
  https://github.com/NavalCommand/SeaPower.Client.git
  "D:/Projects/NavalCommand/SeaPower.Client")
# Adapter compilation only; substitute the actual approved installation:
set(SEAPOWER_GAME_ROOT "D:/Games/SeaPower" CACHE PATH "Local compile-only references")
```

Build.CMake itself has a minimal stable bootstrap name, `ncbuild`, and uses CMake's standard `FETCHCONTENT_SOURCE_DIR_NCBUILD` override. All repositories are still local; that bootstrap override is necessary for normal development until the declared NavalCommand Git URLs are published. It does not require an extra preset or an environment-based workspace root. For moving main references, reconfigure to update; use a fresh o/ tree or `cmake --fresh` with the same explicit options when changing bootstrap/source selections. Never edit generated _deps sources. Evidence records the exact source revisions actually tested; main is a moving development reference, not an immutable release pin.

## Presets and build commands

Checked-in CMakePresets.json contains **only configurePresets**. Names are windows-msvc-debug/release and linux-clang-debug/release where applicable; SeaPower.Mod has only Windows presets because its native loader/ABI implementation is Windows-specific. All use Ninja and `o/${presetName}`. Windows target/host triplet is x64-windows-static, with CMAKE_MSVC_RUNTIME_LIBRARY set to MultiThreaded$<$<CONFIG:Debug>:Debug> (/MT Release, /MTd Debug). The original Task 004–005 evidence records the earlier static-md configuration; it remains historical. Linux uses x64-linux. Linux was not executed on this Windows host; its presets are not a portability claim for the game adapter.

Enter the **VS 2026 Insiders x64 developer shell** first. Set standard VCPKG_ROOT and place CMake 4.2+ and Ninja on PATH. The installed MSVC is **19.51.36260**, toolset directory 14.51.36231. Shared target configuration requests C++26 without extensions; the central MSVC CMake-table override maps it to **/std:c++latest**, with /Zc:__cplusplus, exceptions and UTF-8. No NC_CLANG/NC_LINK/NC_CMAKE/compiler-selection environment variables remain. Full evolving C++26 language/library coverage is not claimed.

From each native/managed module root:

```powershell
cmake --preset windows-msvc-release -DFETCHCONTENT_SOURCE_DIR_NCBUILD=D:/Projects/NavalCommand/Build.CMake
cmake --build o/windows-msvc-release --parallel 8
ctest --test-dir o/windows-msvc-release --output-on-failure --no-tests=error
```

Each command builds independent module targets, fetching/substituting dependencies through the same resolver. Only the current top-level module registers its tests; applications remain available when composed, preserving the CLI/tools required by Mod/debugging. The small module Build-Native.ps1 wrappers configure then delegate actual build/test execution to the central runner.

External packages remain vcpkg manifest/pinned source dependencies. The baseline is unchanged: `3ddaad9be959816602453ecb05533f8732464ef4`, protobuf/protoc 33.4, gRPC 1.81.1, Google.Protobuf 3.33.4. To reuse an already source-built prefix, add standard cache options `-DVCPKG_INSTALLED_DIR=ABSOLUTE_PREFIX -DVCPKG_MANIFEST_INSTALL=OFF` at initial configure. Task 005 reused Task 004's completed source-built prefix; it did not download unmanaged binary packages or repeat an uncached dependency build. Future clean provisioning remains scripts/Prepare-Dependencies.ps1 with binary cache disabled. That script now provisions x64-windows-static. When migrating, configure with --fresh and a prefix containing the new triplet, then rebuild all native targets; do not reuse the earlier static-md artifacts. Normal default configuration uses vcpkg manifest installation.

Mod managed build/packaging remains:

```powershell
./scripts/Build.ps1 -GameRoot ACTUAL_APPROVED_INSTALL -BuildCMakeSource ACTUAL_BUILD_REPOSITORY
```

The package remains `SeaPower.Mod/out/package/0.1.0` and excludes proprietary references/platform Span DLLs. Configure output is now o/windows-msvc-release; CLI is `Gateway.Grpc.Api/o/windows-msvc-release/app/naval-cli.exe`, mock host is `Gateway.Grpc/o/windows-msvc-release/app/mock-host.exe`. Standalone managed API codegen remains `dotnet build managed/Gateway.Api.csproj -p:Protoc=ACTUAL_SOURCE_BUILT_PROTOC`. Installation defaults were removed from tracked projects/scripts: pass GameRoot explicitly, or use the ignored SEAPOWER_GAME_ROOT cache value for independent provider compilation.

Existing end-to-end regression commands, from the parent checkout after building:

```powershell
$cli=(Resolve-Path Gateway.Grpc.Api/o/windows-msvc-release/app/naval-cli.exe).Path
$mock=(Resolve-Path Gateway.Grpc/o/windows-msvc-release/app/mock-host.exe).Path
./SeaPower.Mod/scripts/Test-Mock.ps1 -HostExecutable $mock -Cli $cli -OutputDirectory NEW_MOCK_DIRECTORY
./SeaPower.Mod/scripts/Start-Live.ps1 -GameRoot ACTUAL_APPROVED_INSTALL -Cli $cli -OutputDirectory NEW_WAYPOINT_DIRECTORY -Smoke
./SeaPower.Mod/scripts/Start-Live.ps1 -GameRoot ACTUAL_APPROVED_INSTALL -Cli $cli -OutputDirectory NEW_PROTECTED_DIRECTORY -Scenario (Resolve-Path SeaPower.Mod/app/harness/AircraftThreat.ini) -Blocked
./SeaPower.Mod/scripts/Start-Live.ps1 -GameRoot ACTUAL_APPROVED_INSTALL -Cli $cli -OutputDirectory NEW_RELOAD_DIRECTORY -ReloadTest
```

Use new directories and allow the preceding owned game process to finish exiting before the next invocation. Normal CLI use remains read-only. The wrappers retain exact backup/restore and port/readiness/error checks; their runtime behavior and Task 004 fixture scope are unchanged.

## Verification results

| Check | Actual execution | Result/evidence |
|---|---|---|
| Before-layout/targets | git ls-files and original root target extraction | before-layout.json |
| Independent clean project builds | New o/windows-msvc-release directories; final cmake --build MODULE/o/windows-msvc-release --clean-first --parallel 8 and ctest for each affected module with VS 2026 MSVC | *-clean-build.log and module test logs; codec, imports, gateway, provider, host ABI pass |
| Build.CMake independent validation | Configure preset; CTest ResolutionTest.py | resolver-tests.log: Git, local/direct/transitive, mixed, invalid/conflict/late/tag, canonical IDs/shortening pass |
| Canonical serialization/schema | Existing C# write → new MSVC C++ roundtrip → C# read; existing Check-Schema | conformance.log passes |
| Same mock protocol | Existing Test-Mock with new MSVC host and independent CLI | mock/ receipts, synthetic movement and reconnect pass |
| Live native waypoint | Existing Start-Live -Smoke with refactored composed MSVC package | live/ receipts: native creation, 5.159148878 m displacement, invalid requests and duplicate reconnect pass |
| Protected and reload regression | Existing AircraftThreat -Blocked and -ReloadTest | protected/ and reload/ receipts |
| Whole-module Git resolution without local file | Fresh Mod clone, no Build.Local.cmake, no bootstrap SOURCE_DIR; actual Git fetch for Build.CMake/API/service/gateway/provider | git-configure/build/tests/package logs; Git main declarations and ABI test pass |
| Tracked structure/source audit | Source-body/schema preservation, roots/target placement/preset shape/ignored overrides/path/helper audit | source-preservation.json, structure-audit.json, final revisions |
| Vendor safety/normal launch | Rehash all 233 Task 002 vendor files; exact own helper and Doorstop restore | installation-check.json/vendor-hashes.json |

Because the NavalCommand repositories remain unpublished, the whole-module Git test used disposable **local bare mirrors** and an isolated temporary Git `url.insteadOf` configuration. CMake still declared the required HTTPS URLs and actual FetchContent cloned them at main; no Build.Local.cmake or source-dir override existed anywhere in that graph. This verifies the Git code path without publishing or claiming those remote URLs currently host the modules. The ordinary developer build uses the ignored registration files as requested. Temporary Git transport rewriting is test infrastructure, not committed product configuration.

An early structural configure exposed imported-target scope across new subdirectories; shared CMAKE_FIND_PACKAGE_TARGETS_GLOBAL fixed it. CTest initially lacked a top-level testfile because enable_testing was hidden inside function scope; module composition now uses a macro and the runner rejects empty test suites. ABI testing initially couldn't find its relocated host DLL; its explicit test-only loader path fixed that. A back-to-back live preflight briefly saw the preceding test process closing; it correctly refused replacement and reload was rerun after exit. No runtime source workaround was needed. Known generated/gRPC and managed platform-reference warnings remain.

Task 004's limitations remain: AirOps launch is partial, snapshots are not generally atomic, active strategic-AI command authority is unverified, waypoint displacement is not arrival, and graceful game-exit native shutdown is unverified because the existing cleanup wrapper force-closes its owned process. The development loader remains unofficial and inactive after tests. No loader installation, vendor binary changes, platform bypass, runtime worker change or new control capability was introduced.

Final source repositories are clean and committed; exact revisions are in structure-audit.json. The final installation check found all 233 vendor files unchanged, the exact original own helper restored, Doorstop inactive and zero remaining game processes. Generated distribution artifacts are under SeaPower.Mod/out/distribution/Task005: six complete-history source bundles and the tested versioned package ZIP, with hashes in distribution-manifest.json. Local developer files are intentionally excluded from bundles; recreate them using the documented registrations and bootstrap override.
