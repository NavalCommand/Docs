# Module structure and build system

**Authority:** this document specifies the long-lived repository, dependency, CMake and build conventions. See [ARCHITECTURE.md](ARCHITECTURE.md) for module responsibilities, [LOCAL_SOURCES.md](LOCAL_SOURCES.md) for untracked source overrides and [CODE_STYLE.md](CODE_STYLE.md) for handwritten code conventions. Historical build reports and benchmark/test evidence are under [Tasks/](Tasks/README.md) and [Evidence/](Evidence/README.md), not in this specification.

## Repository boundaries

The platform has five independent functional Git repositories and one separate infrastructure repository:

| Repository | Responsibility | Build shape |
|---|---|---|
| `Gateway.Api` | Canonical Protobuf payloads, provider-facing interfaces, transport-independent domain semantics/adapters | Native C++ libraries; generated C# bindings |
| `Gateway.Grpc.Api` | External gRPC services importing canonical `Gateway.Api` payloads; development CLI | Native C++ bindings and executable |
| `Gateway.Grpc` | Generic, provider-injected C++ gateway; separately composable mock-host | Native library, mocks, tests and executable |
| `SeaPower.Client` | Game-specific C# facade/provider, game-thread validation and observation | Managed compile-only game integration, local fake tests |
| `SeaPower.Mod` | C# composition/loader root, native host/C ABI, DI, package assembly and developer harness | Managed/native build and packaging orchestration |
| `Build.CMake` | Shared CMake functions, policies, internal-source registry and native target configuration | Infrastructure only; never a runtime/domain module |

The provider and gateway implementations do not depend on each other. `SeaPower.Mod` binds them. The common `.proto` payload types are defined once in `Gateway.Api`, imported by `Gateway.Grpc.Api` and generated for both C++ and C#. Build/source dependency edges must not contradict [ARCHITECTURE.md](ARCHITECTURE.md).

## Native repository layout

Model the layout on `UnrealVoxelSim/ModuleTemplate` and preserve PiSubmarine-style `.Api`/composition boundaries:

```text
Module/
├── CMakeLists.txt                 # initialization, shared helper, dependencies and composition only
├── CMakePresets.json             # committed, configure presets only
├── Build.Local.cmake             # OPTIONAL: gitignored on this developer's machine
├── public/NavalCommand/...       # exported headers, namespaces mirrored by directories
├── src/CMakeLists.txt            # production or INTERFACE library targets
├── mock/CMakeLists.txt           # reusable mock targets, if necessary
├── test/CMakeLists.txt           # tests, if necessary
├── app/CMakeLists.txt            # command-line/standalone applications, if necessary
└── benchmark/CMakeLists.txt      # benchmarks, if necessary
```

Create only the directories a module needs. Place classes and interfaces in purpose-specific files and maintain namespace-mirrored source/header paths. The root may declare its `project()`, options, shared `Build.CMake` bootstrap and call centralized module composition, but **must not define buildable targets** with `add_library()` or `add_executable()`. Native target declarations belong to their respective subdirectory `CMakeLists.txt`. Managed C# projects retain appropriate `.csproj` conventions; their CMake entry points, if present for composition, do not force C# source into native layout conventions.

Public C++ targets must expose only necessary include paths and dependencies. Keep mocks/tests and runtime tooling out of a production library's public dependency surface. Avoid circular dependencies, duplicated shared helpers and empty speculative modules.

## Centralized `Build.CMake`

Every native root fetches and includes a versioned `Build.CMake` repository using CMake `FetchContent`; the shared repository owns:

- Project-wide target configuration, C++26 compiler flags, warnings and native build options.
- Consistent optional `src/`, `mock/`, `test/`, `app/`, `benchmark/` subdirectory composition.
- Internal `NavalCommandAddDependency(URL TAG)` registration and global source identity/override state.
- Optional `NavalCommandSetDependencyPath(URL PATH)` resolution via ignored `Build.Local.cmake`.
- Canonical, case-normalized repository identities and deterministic compact hashed `FetchContent` names to limit Windows dependency path length.
- `CMP0168 NEW` and compatible CMake policy handling.
- Testing/build runner utilities where broadly reusable.

Do not vendor or duplicate common CMake scripts into functional modules. Do not use CPM.cmake. Internal source dependencies use the central helper; external packages use **vcpkg manifest mode**, with approved pinned versions and source-built artifacts. Do not hardcode local checkout paths in tracked files. For details see [LOCAL_SOURCES.md](LOCAL_SOURCES.md).

Local substitutions must occur **before the first `FetchContent_Declare`** for a canonical identity; normal Git fetches retain the source URL/ref provided in committed modules. Validate missing paths and conflicting registrations. Preserve transitive reuse and Windows hashed fetch names independent of source selection.

## Presets, compilers and runtime linkage

Checked-in `CMakePresets.json` files contain **only `configurePresets`**; there are no `buildPresets` or `testPresets`.

- Non-hidden preset naming: `<platform>-<compiler>-<build type>`, e.g. `windows-msvc-debug`, `windows-msvc-release`, `linux-clang-debug`, `linux-clang-release` where supported. Hidden base presets may end in `-base` and exist solely for inheritance.
- Use Ninja and preset-separated binary directories `o/${presetName}`. Select native compilers from the initialized developer toolchain, not project-specific `NC_CLANG` or `NC_LINK` environment variables.
- **Windows: `VCPKG_TARGET_TRIPLET=x64-windows-static` and `VCPKG_HOST_TRIPLET=x64-windows-static`.** Use the matching **static MSVC runtime**: `/MT` in Release and `/MTd` in Debug, via `CMAKE_MSVC_RUNTIME_LIBRARY=MultiThreaded$<$<CONFIG:Debug>:Debug>` or equivalent shared target policy. Do **not** use `x64-windows-static-md` or `/MD` for this project configuration.
- Target C++26, using VS 2026 MSVC `/std:c++latest` for currently supported C++26 features. Do not claim complete standard conformance; document unsupported facilities. Linux Clang builds use a suitable available standard flag/library but Sea Power hosting remains platform-specific.
- Existing build directories or external dependency installations prepared for a different vcpkg triplet/runtime must **not** be reused as evidence of a correct static-runtime build. Reconfigure from a fresh binary directory and rebuild dependencies for the selected triplet; verify the actual compile/link flags.

The native host/C ABI exchanges caller-owned or copied serialized byte buffers; do not share MSVC CRT allocation ownership across the managed/native or module boundary. Record and test lifetime and memory ownership assumptions.

Typical Windows developer commands, from a module root after initializing the VS 2026 x64 developer shell, setting `VCPKG_ROOT`, and making CMake/Ninja available:

```powershell
cmake --preset windows-msvc-release
cmake --build o/windows-msvc-release --parallel 8
ctest --test-dir o/windows-msvc-release --output-on-failure --no-tests=error
```

For local development of `Build.CMake`, use the standard one-off `-DFETCHCONTENT_SOURCE_DIR_NCBUILD=...` override described in [LOCAL_SOURCES.md](LOCAL_SOURCES.md). In application/deployment repositories, preserve independently buildable native targets and use the composition root for final distribution assembly.

## Engineering and verification rules

- Keep C++ production/test/app targets independently buildable. Test the mock/standalone gateway without Sea Power installed.
- Build once with local checkouts and once through normal Git resolution when changing dependency logic; test transitive/repeated, invalid and conflicting local registrations.
- Validate fixed source/schema versions, generated C# and C++ message conformance, gateway mocking, Sea Power live integration and game vendor-file integrity when affected.
- Preserve original native game files, opt-in loader behavior and original normal launch configuration.
- All handwritten C++/C#/CMake/scripts/tests must satisfy [CODE_STYLE.md](CODE_STYLE.md). Generated code, vendored files and installed game binaries are not formatting targets.
- Source/revision/hash/test evidence belongs under `Evidence/` or an appropriately scoped project history folder, not in this normative document.
