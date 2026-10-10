# Task 010 — Managed module layout, CMake/MSBuild integration, and unified outputs under `o/`

**Type:** Project-wide build architecture and source-layout maintenance task. **Source baseline:** The actual latest GitHub/local checkouts; in particular `SeaPower.Mod` source tree `4fed009aeb78d9ae3fb0bf215fc8856603831bbf` observed on 2026-10-10. **Not a gameplay feature task.**

## Objective

All **repository-owned generated build artifacts**, whether from CMake, MSBuild/.NET, Protobuf generation, test compilation, or distribution staging, must be placed under that **repository's own root `o/`** directory. Do not create `bin/`, `obj/`, `out/`, `packages/`, `vcpkg_installed/`, or compilation products beside handwritten source. The previously established native CMake directory `o/${presetName}/` remains unchanged. Use **`o/managed/` as the root of managed outputs**. Production runtime, harness and tests must have separate build outputs to prevent collisions.

Preserve independent Git repository boundaries. A managed-source relocation inside an existing repository is required where C# is currently mixed into the native `src/` tree; do not invent new repositories or introduce an unsynchronized workspace-root output directory. A user-selected external run-evidence directory, the user's game installation, the OS's NuGet cache, and explicit requested exports are not repository build artifacts and are not implicitly relocated.

Suggested per-repository layout:

```text
SeaPower.Mod/
  o/
    windows-msvc-debug/              # existing CMake preset output; actual configured preset names remain authoritative
    windows-msvc-release/
    managed/
      SeaPower.Mod/
        obj/<Configuration>/<TargetFramework>/...
        bin/<Configuration>/<TargetFramework>/...
      SeaPower.Harness/
        obj/<Configuration>/<TargetFramework>/...
        bin/<Configuration>/<TargetFramework>/...
      RuntimeTests/
        obj/<Configuration>/<TargetFramework>/...
        bin/<Configuration>/<TargetFramework>/...
    package/0.1.0/                   # production staging, no harness
    harness/0.1.0/                   # development-only staging

SeaPower.Client/o/managed/SeaPower.Client/{obj,bin}/...
Gateway.Api/o/managed/Gateway.Api/{obj,bin}/...
Gateway.Api/o/managed/Conformance/{obj,bin}/...
```

The examples are *locations*, not mandatory directory creation. Preserve multi-configuration/framework isolation and source-project identity. A developer-local CMake preset such as `windows-msvc-debug-vs` is valid if locally defined; **do not rename CMake presets** just to match this document. Current publicly pushed `SeaPower.Mod/CMakePresets.json` only lists `windows-msvc-debug` and `windows-msvc-release`.

## Gate A — Find every real output location

Inspect, at minimum:

- `SeaPower.Mod`: `managed/runtime/SeaPower.Mod.csproj`, `managed/harness/SeaPower.Harness.csproj`, `test/managed/RuntimeTests.csproj`, `scripts/Build.ps1`, `Build-Harness.ps1`, `Start-Live.ps1`, `Start-Validation.ps1`, `Build-Native.ps1`, `Build-AnchorChain.ps1`, `test/CMakeLists.txt`, `.gitignore`, `README.md`, and `app/CMakeLists.txt` (`managed-paths.json`).
- `SeaPower.Client`: root `SeaPower.Client.csproj` and all build entry points.
- `Gateway.Api`: `managed/Gateway.Api.csproj`, `test/managed/Conformance.csproj`, Protobuf generation (`$(IntermediateOutputPath)generated`), all build/test entry points.
- Other repositories (`Gateway.Grpc`, `Gateway.Grpc.Api`, `Build.CMake`) for handwritten scripts/targets that emit build artifacts outside `o/`. Change only verified affected output-producing paths.

List the current source-adjacent outputs and `out/` stages. Treat a project source tree as read-only for generated files, even when `bin/obj/` are already gitignored.

## Gate B — Configure MSBuild correctly, including project references

Preferred solution is a **small repository-root `Directory.Build.props` in each repository containing managed projects** (`SeaPower.Mod`, `SeaPower.Client`, `Gateway.Api`). It is imported early for nested projects and uses each repository's own `$(MSBuildThisFileDirectory)`. Configure both `BaseOutputPath` **and** `BaseIntermediateOutputPath`, using `$(MSBuildProjectName)` for distinct projects. Do not set a shared global `BaseOutputPath`/`BaseIntermediateOutputPath` on a single `dotnet build` invocation: global MSBuild properties propagate into ProjectReferences and can cause collisions.

Illustrative, to validate with the installed SDK and actual project graph:

```xml
<Project>
  <PropertyGroup>
    <BaseOutputPath>$(MSBuildThisFileDirectory)o/managed/$(MSBuildProjectName)/bin/</BaseOutputPath>
    <BaseIntermediateOutputPath>$(MSBuildThisFileDirectory)o/managed/$(MSBuildProjectName)/obj/</BaseIntermediateOutputPath>
  </PropertyGroup>
</Project>
```

Set the properties before .NET SDK targets derive `MSBuildProjectExtensionsPath` and other intermediates. Retain SDK-created configuration and target-framework subdirectories. Ensure that NuGet assets, generated assembly info, `.deps.json`, caches, `.pdb`, and C# generated from canonical Proto sources all land under the expected `o/managed/<project>/` tree. Verify that building `SeaPower.Mod` transitively builds `SeaPower.Client` and `Gateway.Api` **in their respective repository-local `o/managed/` trees**.

Use evaluated MSBuild `TargetPath`, or another single canonical project output-property, for scripts that copy managed DLLs. Avoid hardcoded source-adjacent `managed/.../bin/Release/netstandard2.1` and brittle assembly-name assumptions. A build script must fail clearly when the expected output is unavailable, rather than silently copying a stale DLL.

## Gate C — Move package/staging output under `o/`

- Move `SeaPower.Mod/out/package/0.1.0` to `SeaPower.Mod/o/package/0.1.0`.
- Move `SeaPower.Mod/out/harness/0.1.0` to `SeaPower.Mod/o/harness/0.1.0`.
- Update `Build.ps1`, `Build-Harness.ps1`, `Start-Live.ps1` defaults, relevant documentation and any other affected wrapper/test script; preserve separate runtime and harness manifests and their checksum checks.
- Keep production allowlist untouched in meaning: production stage contains no harness, scenarios, task probes, game assemblies or duplicate Unity/`System.*` shims.
- Preserve stage freshness and reversible loader/plugin deployment. When replacing a previous stage, never delete a path that is not verified to be inside the repository's own `o/`. Do not change live game installation paths or cleanup ownership rules.
- `test/CMakeLists.txt` currently uses `dotnet run --project ...`; verify that test-triggered restoration and compilation also use the new `o/managed/` paths without relying on prior manual setup.
- Do not conflate CMake's per-preset outputs with MSBuild's own configuration folders; a native Debug preset may currently invoke a Release managed build. Document this independently and make the mapping explicit if modified, without introducing incidental ABI/release-mode changes as part of a path-only patch.

## Gate D — Establish CMake + MSBuild integration and managed-source ownership

**Mandatory project-wide policy:** Read and implement the durable rules in `Docs/MANAGED_BUILD.md`, and update `Docs/MODULE_STRUCTURE.md` to reference them. The target is **CMake/Build.CMake for dependency-source resolution and full-build orchestration; native CMake for C++; ordinary SDK-style projects/MSBuild for C#**. Do not generate a competing C# build graph or require developers to run CMake on every IDE build.

### D1. Sweep and reorganize real handwritten managed code

Inspect every C# source, `.csproj`, generated-code rule, C# test project and CMake entry point in all six source/infrastructure repositories. At minimum:

- `SeaPower.Client` currently has its `SeaPower.Client.csproj` in the repository root and C# implementation under `src/NavalCommand/SeaPower/Client/`. Move them into `managed/`, e.g. `managed/SeaPower.Client.csproj` and `managed/src/NavalCommand/SeaPower/Client/*.cs`. Keep native `src/` available only if actual C++ targets exist; don't manufacture empty native modules. Move/update managed test project references under `test/managed/` if appropriate, with focused build/solution compatibility checks.
- `Gateway.Api` already has `managed/Gateway.Api.csproj`, and `Gateway.Api/test/managed/Conformance.csproj` consumes it. Preserve canonical Proto files under `proto/`; generated C# remains in `o/managed/Gateway.Api/obj/...`.
- `SeaPower.Mod` already has `managed/runtime/` and `managed/harness/`; relocate `app/CMakeLists.txt` managed discovery and configuration into `managed/CMakeLists.txt`, while preserving true native/application composition as appropriate.
- Check the remaining repositories for managed code before declaring that no migration is needed. Source moves must retain namespaces/assembly names, game/native references, Protobuf semantics and historical tests.

### D2. Reusable shared CMake managed-project registration

Extend the **existing** shared `Build.CMake` module-composition logic to discover `managed/CMakeLists.txt` whenever a repository contains it. Provide a small reusable helper or registration API for managed project metadata and CMake build/packaging targets. Keep dependency fetch identities, `NavalCommandAddDependency()`/FetchContent, optional `Build.Local.cmake` and resolved source paths the **sole authoritative internal repo resolver** for both languages. Never add a separate C# Git resolver or assume workspace sibling paths.

MSBuild must remain responsible for SDK-style C# project-to-project dependencies (`ProjectReference`) and transitive recompilation; CMake should **not** schedule redundant simultaneous builds of every transitive C# library while a consumer MSBuild graph is already compiling it. Where CMake exposes standalone managed module targets, define clear dependencies that avoid collision and duplicate work.

At CMake **configure** time, generate the local MSBuild project context needed for build-time resolution: actual dependency checkout paths, resolved protoc path, game reference paths when configured, and output locations. Use an optional gitignored generated `.props`/`.targets` import or equivalent IDE-compatible mechanism with stable committed `.csproj` files; no absolute local paths committed to source. The generated metadata belongs under a repo-owned `o/managed/` path. Do not overwrite checked-in `.csproj` files or create competing wire schemas.

**Direct IDE build gate:** after one successful CMake configure, open `SeaPower.Client` and `SeaPower.Mod` managed projects in Rider or Visual Studio and build the normal `.csproj`/solution via native MSBuild or `dotnet build` **without re-running CMake, manually setting `-p:GatewayApiSource`, `-p:SeaPowerClientSource`, `-p:Protoc`, etc., or copying dependent DLLs**. MSBuild must follow real `ProjectReference`s and generate canonical C# protobuf bindings when appropriate. Identify exactly which configure-time inputs cause regeneration (source checkout changes, game SDK location, compiler/protoc selection), as distinct from editing a C# file. Test local override and normal Git FetchContent checkout cases; don't merely assert that a generated project loads.

Handle multiple CMake configurations and consumers without silently reusing a stale active IDE context. Choose a small documented, verifiably reliable context selection strategy rather than introducing a new tool ecosystem or speculative `.sln` generator. A generated `.sln`/`.slnx` may improve UX, but standalone `.csproj` buildability after configure is the mandatory acceptance requirement.

### D3. Orchestrate full builds and staging from CMake

Provide one coherent entry point for each complete module. `cmake --build o/<preset> --target package-runtime` must build the native host and all required managed projects (through MSBuild's own reference graph), then assemble the production distribution under `o/package/`. `package-harness` independently builds and stages the development harness under `o/harness/`; do not mix it into the production artifact. Native-only build/configure/test paths must continue to work without a game installation and should not demand managed game SDK references unless a game-dependent managed target is requested.

The CMake-selected Debug/Release configuration must reach managed compilation; avoid the current script's hard-coded Release. Support both single-config and supported multi-config generators without changing committed preset names. CMake may call a narrow PowerShell packaging implementation for allowlists/hashes/rollback, but **PowerShell must not own a second dependency/build graph**. Turn existing build wrappers into thin `cmake --build --target ...` helpers or retire them after updating all callers and docs.

### D4. Distinguish configure, IDE and packaging usage in documentation

Document three explicit workflows: (a) one-time `cmake --preset ...` source/tool discovery and generated IDE context; (b) repeated direct IDE/MSBuild compilation and tests on the configured managed projects; (c) CMake full native+managed package build. Include clean environment setup, configuration changes, verification of output directories and opt-in loader deployment. Preserve existing approved BepInEx safety rules.

## Gate E — Regression and cleanup evidence

Show, with actual command output and locations:

1. Clean build of managed runtime, harness, `RuntimeTests`, `SeaPower.Client` and `Gateway.Api`, plus managed Protobuf conformance tests, from source with no old `bin/obj/out` present. Verify every managed `.csproj` can be built directly by MSBuild/VS/Rider **after** one CMake configure, using actual generated dependency context.
2. Native CMake configure/build/CTest continues to work for active preset names, including a chosen Debug and Release configuration. `Build.CMake` resolves transitive native **and managed** checkout paths, for local overrides and ordinary Git dependency resolution. Generated managed context replaces or extends `managed-paths.json` without relying on it as a mandatory separate PowerShell phase.
3. `dotnet build` of the runtime's project graph writes its outputs **only** to each repo's own `o/managed/` (no shared transitive intermediate path/cross-project collisions).
4. CMake `package-runtime` and `package-harness` targets independently produce correct stages under `o/`; show the native → managed → package dependency sequence, managed Debug/Release mapping and separate manifests. Preserve smoke/mock/in-game regression behavior; execute when required and approved, otherwise report *not run*.
5. `git status --short` shows no generated source-adjacent artifacts; a generated-file audit reports no newly produced files outside `o/` within the repositories. Do not confuse preexisting user-owned files with build outputs.
6. Verify existing intentionally ignored `bin/obj/out` remnants separately. Offer a **safe, explicit** cleanup plan scoped to known generated directories; do not recursively delete arbitrary user-owned files or historical evidence.

Update the **permanent** `Docs/MODULE_STRUCTURE.md` to state both the project-wide managed CMake/MSBuild integration rule (link `Docs/MANAGED_BUILD.md`) and the cross-language `o/` output rule, including `o/managed/<project>/{bin,obj}` and `o/package`/`o/harness` for mod staging. Update module READMEs/examples; put execution details, tests and run evidence only under `Docs/Tasks/010/` and `Docs/Evidence/Task010/`. Do not modify archived reports or owner-supplied CodeStyle.xml. All Docs edits go into the separate Git-tracked Docs repository.

## Scope and compatibility

No changes to naval/domain APIs, Proto field numbers, RPCs, native host ABI, game rules, observation visibility, runtime activation names, or production-vs-harness dependency direction. Do not rename `SeaPower.Host` or retarget Windows runtime linkage; retain `x64-windows-static` and `/MT`/`/MTd`. Preserve dedicated independent repository builds, existing CMake conventions and task-agnostic permanent docs. The approved change to `Build.CMake` and moving `SeaPower.Client` C# out of the native `src/` boundary are **in scope**; avoid any unrelated redesign. Do not fix unrelated lifecycle issues during this purely output-layout task unless an actual blocker prevents validation; record them as follow-ups instead.

## Deliverables

- `Docs/MANAGED_BUILD.md` is the permanent managed integration policy. Update `Docs/MODULE_STRUCTURE.md`, index links and module READMEs to align with it; keep Task 010-specific evidence out of permanent root docs.

- Committed implementation in affected code repositories and corresponding canonical Docs policy update; **do not push** unless authorized.
- `Docs/Tasks/010/REPORT.md` with before/after path map and project-graph results.
- Small `Docs/Evidence/Task010/` receipts, exact commands, hashes as appropriate, negative scan for outputs outside `o/`, and any honest blockers.

**Pass condition:** One CMake configuration resolves the canonical cross-repository managed/native source graph; standalone IDE/MSBuild managed builds then work without extra developer steps; a CMake full-module build composes native + managed code and packages it correctly; and all repository-owned artifacts remain under each repository's `o/` without changing production/development boundaries or game safety.
