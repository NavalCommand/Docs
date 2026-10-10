# Managed modules and cross-language build integration

**Authority:** Permanent project-wide architectural/build policy. Read with [AGENTS.md](AGENTS.md), [ARCHITECTURE.md](ARCHITECTURE.md), [MODULE_STRUCTURE.md](MODULE_STRUCTURE.md), and [DEVELOPMENT.md](DEVELOPMENT.md). This is a stable contract, not a task log.

## Objectives and design ownership

NavalCommandFramework uses **CMake + the shared `Build.CMake` repository for discovering/resolving internal source repositories and orchestrating full-module builds**, while **MSBuild/.NET remains the authoritative build engine for managed code**. Do not create a second managed dependency-resolution mechanism or a custom C# compiler pipeline.

1. `NavalCommandAddDependency()` / `FetchContent` resolve **repositories** and their transitive identity, source locations and version overrides, whether they contain native code, managed code, or both.
2. `Build.CMake` provides small **reusable** managed-target registration/configuration helpers; individual `managed/CMakeLists.txt` files describe each repository's managed projects and their external requirements. Do not hardcode Sea Power in the shared helper.
3. Canonical, **source-controlled SDK-style `.csproj` projects and `ProjectReference`s** describe managed compile-time dependencies. MSBuild handles C# compilation, NuGet restore, transitive project builds, target-framework compatibility and incremental compilation; CMake must not manually emulate that graph or independently compile every transitive project.
4. CMake generates the **local MSBuild context** needed to resolve project references, protoc, permitted external game assemblies and output paths. It derives this context from the actual `FetchContent`/`Build.Local.cmake` resolved source locations, not guessed sibling checkouts or hardcoded installation directories.
5. **After one successful CMake configuration**, a developer can open a normal managed `.csproj` in Visual Studio/Rider and build, restore, debug or test it using standard .NET/MSBuild actions. No per-build CMake invocation, extra manual MSBuild `-p` arguments or manually copied dependency files may be required. CMake regeneration is needed when dependency identities, source checkouts or configured SDK paths change, not on every C# edit. Do not require CMake merely to rebuild unchanged managed project references.
6. When building a complete mod distribution, CMake builds required native targets, invokes the relevant top-level managed MSBuild project graph, then stages a **single complete, validated distribution** using dedicated packaging targets. Packaging does not require running a separate build-script dependency resolver.

The source-control/build ownership split is deliberate: CMake owns *source resolution and orchestration*; MSBuild owns *managed project build semantics*. This is **one coordinated build**, not one compiler.

## Project layout

- Reserve `src/` for native C++ sources/targets (where applicable). Put handwritten managed implementation code and corresponding `.csproj` in `managed/`, with `managed/CMakeLists.txt` as its CMake entry point. Keep `.proto` schemas in their existing canonical `proto/` location. Generated code belongs only in intermediate directories.
- Recommended structure for a managed-only provider: `managed/SeaPower.Client.csproj` and `managed/src/NavalCommand/SeaPower/Client/*.cs`. Keep `test/managed/` for managed tests, with explicit project references rather than copied/recompiled production source files where practical.
- `SeaPower.Mod/managed/runtime/` and `managed/harness/` remain independently buildable, deployed separately and never acquire a runtime dependency from the production plugin to the test harness.
- A repository may contain *both* `src/CMakeLists.txt` and `managed/CMakeLists.txt`. The shared `Build.CMake` module composition must discover and configure both when present; never create empty placeholder directories in native-only repositories.
- Application/composition targets still belong in `app/` where appropriate; do not keep an `app/CMakeLists.txt` whose only job is to discover/configure C# code after that role is moved to `managed/`.

## Configuring managed projects for IDE independence

Use the **existing committed `.csproj` files**; do not generate/overwrite project source from CMake. Supply a documented, optional, generated MSBuild context (`.props`/`.targets` or equivalent) via a stable project import or `Directory.Build.props` integration. The mechanism must work when the project is opened directly by an IDE, including when the project source is a local override versus a FetchContent checkout. Avoid required machine-specific absolute paths in committed source. Generated local paths and build contexts are gitignored under the relevant repository's `o/`.

`Directory.Build.props` is a suitable early-import place for `BaseOutputPath`, `BaseIntermediateOutputPath` and an optional generated context. **Pay attention to import order**: `BaseIntermediateOutputPath` must be resolved before the .NET SDK chooses NuGet/intermediate locations; dependency properties must be available when project references are evaluated. A shared `-p:BaseOutputPath=...` argument propagated into all `ProjectReference`s is **not** acceptable because it can collide across project builds. Each repository and each project owns its separate output directory.

Manage concurrent/preset-dependent build contexts explicitly. A project used by Debug and Release or by multiple consuming superbuilds must not silently inherit a stale dependency path from a different configuration. Select a clear active IDE context, or design isolated per-configuration contexts with documented selection; test the actual use in Visual Studio or Rider and with `dotnet build`/`msbuild`. A solution/workspace file may be generated for IDE convenience, but is not a substitute for independently buildable `.csproj` projects.

Do not run `dotnet restore`/`dotnet build` merely to **configure CMake** unless a specific validated setup step requires it. Reuse actual tool paths found by CMake, especially the pinned `protobuf::protoc`. If a project needs game SDK assemblies, permit native-only configuration without them, but fail clearly when a game-dependent managed build is specifically requested.

## Build orchestration and target semantics

- Add minimal managed target helpers to `Build.CMake`; do not duplicate generic implementation inside `SeaPower.Mod` or `SeaPower.Client`. Source dependencies continue to be registered and deduplicated by canonical repository identity.
- Expose meaningful CMake targets such as `managed-runtime`, `managed-harness`, `package-runtime` and `package-harness` where applicable. `package-runtime` depends on the native host and the top-level managed runtime; `package-harness` depends only on the separate developer harness and approved fixtures. Generic helper names are preferable for shared APIs; sample project target names are not a mandatory cross-repository naming schema.
- Building a top-level C# project lets MSBuild follow its `ProjectReference` closure. Do not add a second, parallel CMake compilation of those same managed dependencies. CMake can register a target for independently requested managed-library builds without forcing duplicate compilation during packaging.
- Honor both single-config Ninja and supported multi-config generators: drive C# configuration using the CMake-selected build configuration (e.g. `$<CONFIG>`), not a hardcoded `Release` or `Debug`, and keep architecture/runtime/target-framework conventions intact.
- Keep C++ native ABI, C# P/Invoke, protobuf representation and toolchain settings compatible. C++ host deployment continues to use `x64-windows-static` with `/MT` or `/MTd` as prescribed by [MODULE_STRUCTURE.md](MODULE_STRUCTURE.md).
- MSBuild projects build to each **owning repository's** `o/managed/<project>/{bin,obj}/...`; Protobuf-generated C# belongs in `obj` there. Existing CMake presets continue to build in `o/${presetName}/`. A mod's distribution staging uses `o/package/...` and `o/harness/...` rather than `out/`.
- Use an evaluated MSBuild target output path (or a generated build manifest) when packaging, not a guessed source-adjacent `bin/Release/<tfm>` location. Stage from a clean directory, validate a strict redistribution allowlist, checksum files and preserve separate runtime/harness manifests. No proprietary game assemblies or experimental probes go into the production package.

## Validation and scope limits

Every change to this integration must verify direct IDE/MSBuild compilation **after configuration**, transitive C# project references, direct and transitive local source overrides, ordinary Git resolution, Debug/Release isolation, fresh builds without source-adjacent artifacts, Protobuf generation, CTest integration and production-vs-harness package contents. Mark tooling/IDE scenarios not actually executed as unverified.

Generated paths, build logs, binaries, restore intermediates, and packages remain under `o/`. Ordinary NuGet global caches, deliberately selected external runtime logs and the game installation are not repository-owned build artifacts. Do not move vendor code or change gameplay/contracts merely to standardize build structure.

## Implemented configure, IDE and package workflows

Enter the VS 2026 x64 developer shell with CMake 4.2+, Ninja, .NET SDK (including net10.0 for regression executables), PowerShell 7 and Git available. Set standard `VCPKG_ROOT` for the pinned manifest toolchain. Register developer source substitutions and optional `SEAPOWER_GAME_ROOT` in ignored top-level `Build.Local.cmake`. No installation is needed for native-only targets or the fake-provider/runtime regression executables; a requested game-dependent project gives an explicit error if its compile-only references are unavailable.

Configure once from the owning repository:

```powershell
cmake --preset windows-msvc-release
```

Local work on the shared helper may add `-DFETCHCONTENT_SOURCE_DIR_NCBUILD=/absolute/path/to/Build.CMake`. The helper discovers `managed/CMakeLists.txt`. `NavalCommandManagedContext(Name=value ...)` writes the repository-local ignored context; `NavalCommandAddManagedProject(target project.csproj)` registers an explicit MSBuild build target. `NavalCommandManagedProtoc(output)` uses the resolved Protobuf host tool. Context generation never compiles C# or restores NuGet.

After configuration, repeat ordinary direct builds without CMake or manual properties:

```powershell
dotnet build managed/SeaPower.Client.csproj -c Debug
# From SeaPower.Mod:
dotnet build managed/runtime/SeaPower.Mod.csproj -c Release
dotnet build managed/harness/SeaPower.Harness.csproj -c Release
dotnet run --project test/managed/RuntimeTests.csproj -c Release
```

Open those same ordinary projects in Visual Studio or Rider. Their committed ProjectReferences use CMake-resolved properties; each referenced repository imports its own context and writes its own outputs. Fake-provider tests deliberately retain source links to the engine-independent provider classes so they do not require proprietary game references. Direct .NET verification is distinct from testing interactive IDE restore/design-time behavior.

CMake's selected `$<CONFIG>` reaches managed builds and CTest, including Ninja Multi-Config. Neither compilation nor packaging silently chooses Release for Debug. Full staging uses:

```powershell
cmake --build o/windows-msvc-release --target package-runtime
cmake --build o/windows-msvc-release --target package-harness
ctest --test-dir o/windows-msvc-release --output-on-failure --no-tests=error
# With a supported multi-config build directory:
cmake --build o/multi --config Debug --target package-runtime
```

The `scripts/Build.ps1` / `Build-Harness.ps1` wrappers select the configured CMake target; they do not configure or resolve sources. `-Configuration` is relevant to multi-config generators; a single-config build uses its configured build type. Package records under `o/managed/<project>/bin/<Configuration>/` contain evaluated TargetPath and copy-local paths, including the actual platform/framework directories; SDK binary and intermediate paths retain those dimensions. The current projects have one framework each and the supported native host architecture is x64. Do not concurrently stage different configurations to the same versioned distribution directory; each package invocation replaces it with a fresh stage and checksums all included files.

### Active managed context selection

`o/managed/Build.Context.props` is the explicit active IDE context. Source paths, resolved host protoc, shared MSBuild targets location and configured game SDK directory are its inputs. C# edits, NuGet restore and Debug/Release switches do not require regeneration. Changed dependency resolution, tooling location, repository graph or SDK path requires CMake configuration.

An identical context is not rewritten. A different context fails with a diagnostic; close other builds and select the intended context with `-DNAVALCOMMAND_REPLACE_MANAGED_CONTEXT=ON`. This permission resets to OFF after the successful top-level configure. Reconfigure other consumers before building them. CMake captures the resolved dependency contexts and rejects stale contexts before MSBuild; an IDE uses the explicitly selected active context. Use separate source checkouts for simultaneous incompatible contexts. This is source/tool context selection, not a second managed dependency resolver or extra preset scheme.

The generated import is optional so engine-independent regression projects remain directly usable without configuration. Projects requiring resolved dependencies validate context presence, and game-dependent projects validate their game references. Protobuf codegen uses canonical `proto/` inputs and the resolved host protoc; NuGet remains standard MSBuild restore. `Managed.targets` records outputs after successful compilation, avoiding guessed binary paths or custom package resolution.

### Legacy artifacts and deployment

Audit known ignored `bin/obj/out` remnants before moving or deleting them. Verify canonical containment in the owning repository and absence of tracked/user source; prefer recoverable movement into `o/legacy-generated/`. Never delete arbitrary user-owned directories or historical evidence. Source-generated-file audits exclude `o/` and separately identify explicitly requested receipts/exports. The NuGet global cache and permitted game installation are external inputs, not repository build outputs.

Build/staging does not install or activate plugins. Existing `Start-Live.ps1` deployment remains opt-in, owned-process-only, manifest-validated and reversible. Production packages exclude harness/scenarios, game assemblies, probes and platform System/Unity DLLs. The known Debug gRPC shutdown issue remains a separate unresolved runtime follow-up; build-layout work must not conceal or claim to repair it.
