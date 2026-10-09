# Task 004 reproduction (Windows x64)

Run PowerShell from `C:/Software/GitHub/NavalCommand`. Five subdirectories are independent Git repositories. Do not clone the parent as if it were their source repository. See [TASK004_IMPLEMENTATION.md](TASK004_IMPLEMENTATION.md) for contract ownership, measured results and limits. Current sources and exact Git revisions are listed in [Evidence/Task004/source-revisions.json](../../Evidence/Task004/source-revisions.json).

## Prerequisites and source dependencies

- Existing, approved development installation: `C:/Games/Sea_Power_8_3`, build 261001, existing Anchor Chain/BepInEx and our existing LaunchCandidate plugin. No vendor binary changes or new loader install are required. Steam-disabled package is the user's development package; these scripts do not alter platform binaries.
- Visual Studio 2022 Community C++ tools / clang-cl 19.1.5, Microsoft linker 19.44; CMake 4.2.2 and Ninja on PATH; .NET SDK 10 (final run 10.0.401).
- `C:/vcpkg` checked out at `3ddaad9be959816602453ecb05533f8732464ef4`. Each manifest pins the same baseline. Build external dependencies from source with binary cache disabled:

```powershell
$prefix='C:/Build/NavalCommand/dependencies'
./SeaPower.Mod/scripts/Prepare-Dependencies.ps1 -VcpkgRoot C:/vcpkg -Installed $prefix
$env:VCPKG_ROOT='C:/vcpkg'
$env:NC_VCPKG_INSTALLED_DIR=$prefix
$env:NC_CMAKE='C:/Program Files/JetBrains/CLion 2026.1/bin/cmake/win/x64/bin/cmake.exe'
$env:NC_NATIVE_PRESET='windows-x64-sourcebuilt'
$protoc="$prefix/x64-windows-static-md/tools/protobuf/protoc.exe"
```

The observed dependency build used `vcpkg install --triplet x64-windows-static-md --host-triplet x64-windows-static-md --x-manifest-root=$env:TEMP/naval-task004-deps --x-install-root=$env:TEMP/naval-task004-deps/installed --binarysource=clear`, with the same manifest. A later redundant auto-install was stopped after VSDevCmd replaced VCPKG_ROOT. Final runs reuse the completed source-built package outputs in `$env:TEMP/naval-task004-sourcebuilt-prefix`. This prefix is temporary build infrastructure, not a portable source artifact or binary-package shortcut. The Prepare script above provisions a durable clean prefix instead; it is equivalent to that observed vcpkg invocation, not a claimed second uncached run. Build scripts restore explicit VCPKG_ROOT after importing VS environment. The sourcebuilt preset skips only automatic re-installation of already provisioned dependencies. Keep dependency paths consistent.

Internal pins use local Git URLs in this checkout. For relocation, transport all five repos with Git clones/bundles and set `NC_REPOSITORY_BASE` to their parent **using forward slashes** (or a Git server base URL). No sibling include or direct sibling project reference is used; each build fetches its pinned dependencies. Pins must be accessible in those repos. Do not strip Git history to copy only their latest files.

## Independent builds and composition

```powershell
./Gateway.Api/scripts/Build-Native.ps1
./Gateway.Grpc.Api/scripts/Build-Native.ps1
./Gateway.Grpc/scripts/Build-Native.ps1
./SeaPower.Client/scripts/Build.ps1 -Protoc $protoc
./SeaPower.Mod/scripts/Build.ps1 -Protoc $protoc
```

Each native script configures, builds and runs its own ctest preset. Provider build runs fake-game tests without Unity execution. Mod native tests exercise ABI bounds, duplicate IDs, backpressure, reset and stop/restart. Mod fetches/pins the managed sources, invokes native CPM composition, compiles provider/root and the opt-in helper, and creates `SeaPower.Mod/out/package/0.1.0`. Compiler warnings for protobuf generated offsetof and harness platform-reference conflicts remain; tested build errors are zero.

Actual clean-clone proof used `$env:TEMP/naval-task004-clean-repositories`, cloned each named repository, set the forward-slash NC_REPOSITORY_BASE to that directory, and ran the same five commands inside those clones. These were fresh project builds sharing the already source-built external prefix. No prebuilt module DLL was copied into their build graph. A first relocation attempt used Windows backslashes and CMake failed with Invalid character escape '\U'; forward-slash Git URLs resolved it.

Canonical-schema conformance:

```powershell
dotnet run --project Gateway.Api/test/managed/Conformance.csproj -c Release "-p:Protoc=$protoc" -- write "$env:TEMP/task004-csharp.bin"
./Gateway.Api/out/native/schema-roundtrip.exe "$env:TEMP/task004-csharp.bin" "$env:TEMP/task004-cpp.bin"
dotnet run --project Gateway.Api/test/managed/Conformance.csproj -c Release "-p:Protoc=$protoc" -- read "$env:TEMP/task004-cpp.bin"
./Gateway.Api/test/Check-Schema.ps1
```

C#→C++→C# checks oneof, UTF-8 IDs, degrees, uint64 greater than JSON's exact integer range, unknown field preservation; managed checks optional zero and unknown enum. Native codec tests and the service-import test cover domain mapping and canonical descriptors.

## Mock and live commands

```powershell
$cli=(Resolve-Path Gateway.Grpc.Api/out/native/naval-cli.exe).Path
./SeaPower.Mod/scripts/Test-Mock.ps1 -HostExecutable (Resolve-Path Gateway.Grpc/out/native/mock-host.exe) -Cli $cli -OutputDirectory "$env:TEMP/task004-new-mock"
# Read-only manual client, requires an already running host:
& $cli --endpoint 127.0.0.1:50051
# Each live wrapper starts and then closes its OWN game process:
./SeaPower.Mod/scripts/Start-Live.ps1 -Cli $cli -OutputDirectory "$env:TEMP/task004-new-waypoint" -Smoke
./SeaPower.Mod/scripts/Start-Live.ps1 -Cli $cli -OutputDirectory "$env:TEMP/task004-new-protected" -Scenario (Resolve-Path SeaPower.Mod/app/harness/AircraftThreat.ini) -Blocked
./SeaPower.Mod/scripts/Start-Live.ps1 -Cli $cli -OutputDirectory "$env:TEMP/task004-new-reload" -ReloadTest
```

Use a **new** output directory each run; stale readiness files are not reusable. No menu navigation is needed. Default CLI reads only; `--smoke` opts into one waypoint and invalid-command probes, `--blocked` opts into one command intended for rejection, `--reload` is a test-only stale-session probe. The wrapper's ReloadTest flag asks the isolated helper to call native UnloadMissionAndReload; the public service has no reload operation. Mock listens on 50052; live package config on 50051. Busy port/existing game is rejected. The wrapper verifies package hashes, readiness and timeouts and reports nonzero CLI status as failure. CLI independently reconnects in the waypoint test and retries the same request ID.

The fresh-clone package and CLI were also used for the final live waypoint; evidence records its manifest/native DLL hash, session, entity, request, operational/wall times, native creation and movement. `launch.json`, `ready.json`, `gateway-ready.json`, `gateway.log`, `cli.log`, `smoke-result.json`, and `cleanup.json` provide the bounded receipts. Protected and reload results are separate fixtures/runs; their coverage must not be conflated with waypoint completion or launch.

## Package, install and rollback

The development package includes own managed root/provider/API/helper, permitted Google.Protobuf runtime, static-linked own SeaPower.Host.dll, third-party license notices, config and checksum manifest. Vendor-managed references are compile-only. Never copy System.Memory/System.Buffers/System.Runtime.CompilerServices.Unsafe NuGet DLLs into the game: Unity supplies its platform shims and mixing Span identities broke mission loading in an earlier attempt.

The tested package is also archived at `SeaPower.Mod/out/distribution/SeaPower.Mod-0.1.0-win-x64.zip` (SHA-256 `94AFAA89355256A6399C830769296F5CC32D9FC525BB96FD310CBCB16D00E702`). The same directory contains verified complete-history Git bundles for all five source repositories. Clone each bundle with `git clone PATH/Name.bundle DEST/Name`, set the forward-slash NC_REPOSITORY_BASE to DEST, and use the build commands above. Package/bundles are generated ignored artifacts; source commits and bounded evidence are the durable record.

Configuration is inactive by default (`enabledByDefault=false`, `clientAutoLaunch=false`). The verified deployment route is the existing approved helper loading the external absolute package path. Runtime activation uses NC_TASK004_ENABLED and absolute managed/native paths; this is an opt-in development deployment, not universal automatic plugin installation. All package/native files remain outside the game folder. Neither arbitrary clients nor a scheduler are launched by the runtime.

Start-Live temporarily replaces **only our own** `BepInEx/plugins/LaunchCandidate.dll`, backs up its exact bytes to a uniquely named temporary file, enables the preexisting loader through the reused launcher, then closes its owned Sea Power process, waits for file unlock and restores the exact bytes/environment/config. It refuses to replace an existing game session. Removing the external package and own test output is sufficient after restore; never delete game vendor files. KeepRunning is deliberately rejected by this cleanup wrapper.

If a process/file-lock interruption prevents restore, stop only the process identified in that run's launch.json after checking its executable path, restore LaunchCandidate.dll from the exact backup named by that run's cleanup/error output, and restore pre-run doorstop_config.ini. Do not synthesize another baseline plugin. Final checksums and inactive loader status are in installation-check.json. Final vendor rehash compares the entire Task 002 baseline.

Mission unload invalidation passed, but the running gRPC DLL is retained until process exit. Do not FreeLibrary it under active threads. The worker owns Stop; Unity callbacks do not join it. Off-game explicit shutdown/restart passed. Live cleanup force-terminates owned runs, so graceful game exit is a remaining lifecycle validation gap. A native in-process crash can terminate the game.
