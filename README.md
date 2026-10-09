# NavalCommandFramework documentation

This `Docs/` directory separates **normative project contracts** from **dated implementation/investigation records**. The files at the root define long-lived engineering rules; work history lives in subdirectories.

## Permanent project documents

| Document | Purpose |
|---|---|
| [AGENTS.md](AGENTS.md) | Sole project-wide instructions for contributors and coding agents; explicitly read this file from code repositories rather than copying it to the unsynchronized parent workspace |
| [ARCHITECTURE.md](ARCHITECTURE.md) | System concepts, domain ownership, observation boundaries, native automation and deployment architecture |
| [MODULE_STRUCTURE.md](MODULE_STRUCTURE.md) | Git repository separation, shared `Build.CMake`, module structure, toolchains, presets, dependency and runtime linkage policy |
| [LOCAL_SOURCES.md](LOCAL_SOURCES.md) | Optional, Git-ignored `Build.Local.cmake` internal dependency overrides |
| [CODE_STYLE.md](CODE_STYLE.md) | Mandatory source readability and style requirements in all handwritten languages |
| [CodeStyle.xml](CodeStyle.xml) | Original CLion/JetBrains C++ style export; retain verbatim |

The current intended Windows native dependency configuration is **`x64-windows-static`** with `/MT` (Release) or `/MTd` (Debug). Consult [MODULE_STRUCTURE.md](MODULE_STRUCTURE.md) rather than copying compiler flags from historical build reports.

## Historical project records

[Tasks/](Tasks/README.md) contains assignment specifications, investigation reports, execution/reproduction guides and implementation reports grouped by numbered work item. These are **historical source documents**: their commands, paths, dependency triplets, testing status and build outcomes describe the environment at the time of execution and are not normative for new work.

[Evidence/](Evidence/README.md) retains the original machine-readable runtime logs, hashes, fixtures and test results. Preserve those files as evidence rather than reformatting them to match new code conventions. Changes to durable architectural decisions should be reflected in the permanent documents, not appended as a dated progress note.

## Maintaining this documentation

- Update the corresponding normative document when a stable design, API, code-style or toolchain decision changes.
- Store new assignments, work reports, run-specific reproduction instructions and measurements under `Tasks/` and `Evidence/`.
- Keep paths relative for navigability. Avoid hardcoded developer installations or absolute source paths in permanent files, except explicitly fictional examples.
- Do not erase historical results when the preferred configuration changes. The archived output remains a record of what was actually tested.
