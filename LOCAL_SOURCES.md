# Local checkout overrides

This document describes the optional, developer-owned `Build.Local.cmake` mechanism implemented by the shared `Build.CMake` infrastructure. It never changes a module's committed dependency declarations.

## Committed dependency declarations

Native module roots register actual internal repository dependencies with the shared resolver:

```cmake
NavalCommandAddDependency(
    "https://github.com/NavalCommand/Gateway.Api.git"
    "<approved-git-ref>"
)
```

Use a pinned commit/tag for reproducible release builds. Branch refs are suitable for deliberate development integration only. The shared helper resolves dependencies using canonical repository identities and a short, deterministic hashed FetchContent name.

## Optional local file

A developer may place `Build.Local.cmake` **alongside the top-level module's `CMakeLists.txt`**:

```cmake
# Build.Local.cmake — never commit
NavalCommandSetDependencyPath(
    "https://github.com/NavalCommand/Gateway.Api.git"
    "D:/Projects/NavalCommand/Gateway.Api"
)

NavalCommandSetDependencyPath(
    "https://github.com/NavalCommand/Gateway.Grpc.Api.git"
    "D:/Projects/NavalCommand/Gateway.Grpc.Api"
)
```

The shared helper defines `NavalCommandSetDependencyPath` and evaluates **only** `${CMAKE_SOURCE_DIR}/Build.Local.cmake` once, before any normal dependency declaration. Keep `/Build.Local.cmake` in every module's `.gitignore`; never commit machine-specific absolute paths, local checkout discovery or overrides in a published `CMakeLists.txt` or `CMakePresets.json`.

The normal `cmake --preset windows-msvc-debug` command works with or without this file. No alternative preset, source-root environment variable or automatic sibling discovery is needed.

## Resolution rules

- A registered canonical URL is populated directly from the existing checkout through `FetchContent` `SOURCE_DIR`; its Git tag is *not* applied to, or enforced on, the local working tree.
- Unregistered dependencies use their committed URL and tag through normal Git-based `FetchContent`.
- Overrides apply globally to direct and transitive dependencies of the configured top-level module.
- A missing, invalid or conflicting explicitly registered path is an error, not an invitation to fetch remotely.
- A second request for the same canonical dependency reuses the same source and target. Conflicting refs or late override changes must fail with actionable diagnostics.
- Only the top-level developer file is loaded; local files inside fetched submodules are not evaluated as new configuration sources.
- Removal or alteration of mappings may require a fresh build directory or `cmake --fresh`; never hand-edit generated `_deps` checkout sources.

## Shared build-system bootstrap

The module's root must fetch/include the `Build.CMake` helper **before** it can evaluate developer-local registrations. Therefore `Build.Local.cmake` cannot substitute the helper itself. For developer work on the helper, use CMake's standard `FETCHCONTENT_SOURCE_DIR_NCBUILD` cache override, supplied externally at configure time:

```powershell
cmake --preset windows-msvc-debug -DFETCHCONTENT_SOURCE_DIR_NCBUILD="D:/Projects/NavalCommand/Build.CMake"
```

Production modules must not require this override when the approved shared build-system Git source is available. Do not publish private local path settings or transport rewrites.
