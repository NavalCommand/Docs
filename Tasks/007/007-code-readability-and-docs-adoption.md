# Task 007 — Code Readability and Documentation Layout Adoption

**Type:** Repository-wide maintenance and quality-control task.  
**Prerequisite:** Use the current working implementations from Tasks 004 and 005 and the current, cleaned `Docs` Git repository. Task 006 may be in progress; **do not assume it is complete**, silently change its semantics, or start unrelated feature work.

## Goal

Bring **all existing handwritten NavalCommandFramework code** into compliance with the project owner's style and readability rules, and adopt the already-completed `Docs` reorganization everywhere the project refers to its documentation.

This task is not another architectural redesign, not a reimplementation of any gateway or game feature, and **not an invitation to rewrite historical reports**.

## 1. Establish the correct documentation authority first

The **`Docs` directory itself is a Git repository**. The parent directory is just a local workspace and is **not synchronized**. All project-wide authoritative documentation belongs inside this tracked `Docs` repository.

Read these files from the **current** `Docs` checkout before editing code:

- `Docs/README.md` — current documentation layout and maintenance policy.
- `Docs/AGENTS.md` — permanent agent and project engineering instructions. **This is the sole project-wide authoritative AGENTS.md in the workspace.** Do not create a competing copy in the unsynchronized parent directory.
- `Docs/ARCHITECTURE.md` — durable architectural requirements.
- `Docs/MODULE_STRUCTURE.md` — repository, CMake and runtime policy.
- `Docs/LOCAL_SOURCES.md` — development-local dependency resolution.
- `Docs/CODE_STYLE.md` and `Docs/CodeStyle.xml` — mandatory handwritten code style. Preserve the XML unchanged.

The `Docs` root contains **only long-lived normative/reference documentation**. Historical tasks, reports and reproduction instructions live under `Docs/Tasks/<number>/`; run evidence lives under `Docs/Evidence/Task<number>/`. Do not restore the old flat `Docs/` layout.

**Important:** The earlier replacement `Docs.zip` has already reorganized this material. Treat the actual current `Docs` Git checkout as authoritative. Do **not** unpack stale documentation packages over it, recreate removed root-level task files, or copy an old `AGENTS.md` above `Docs/`.

### Adopt the reorganization in live project references

Audit active handwritten files in **all six repositories** (`Gateway.Api`, `Gateway.Grpc.Api`, `Gateway.Grpc`, `SeaPower.Client`, `SeaPower.Mod`, `Build.CMake`) for stale documentation paths and assumptions, including READMEs, scripts, build/test instructions, comments, project manifests and developer guides.

- Correct active links to the new locations; for example, historical assignments now live under `Docs/Tasks/004/` or `Docs/Tasks/006/`, not the `Docs` root.
- Keep `Docs/AGENTS.md` as the source of project-wide rules; explicitly read it when working in a code repository rather than assuming an agent automatically discovers an `AGENTS.md` in a different Git repository.
- Do not turn permanent documents into per-task journals. Make **only durable corrections** to root reference documents when a lasting rule/link is genuinely wrong.
- Preserve historical task reports, evidence, source hashes and recorded measurements. Do not rewrite old results or silently replace their historically accurate flags, paths or outcomes. An archival link may remain a historical statement; clarify through the archive index rather than falsifying it.
- Any new task output must be placed under `Docs/Tasks/007/` and any generated evidence under `Docs/Evidence/Task007/`. Only actual software/source changes belong in the respective code repositories.
- Do not create or update a new `AGENTS.md` outside the `Docs` repository as part of this task. Preserve repository-specific configuration that is genuinely needed, but avoid competing project-wide authority.

## 2. Code readability: all handwritten code, not just newly touched files

Perform a **repository-wide audit and cleanup** of tracked, hand-authored code in the six repositories. Include production code, tests, mock implementations, CLI tools, C# game integration, C/C++, CMake, `.ps1`, `.py`, hand-written `.proto`, and scripts/configurations where readability improvements are applicable.

Apply `Docs/CODE_STYLE.md` and the **actual explicit C++ settings** from `Docs/CodeStyle.xml`; the XML must not be interpreted as automatically specifying C# or Python conventions.

Mandatory results:

1. **No compressed/minified handwritten methods or scripts.** Do not pack unrelated statements, chained declarations, guards or test setup/assertions onto one line. A wide line limit does not justify a one-line algorithm.
2. **Use meaningful names.** Replace names such as `la`, `lob`, `a`, `x`, `tmp` when they conceal domain meaning. Prefer names that reveal direction, entity role and units (e.g. `startLatitudeDegrees`, `earthRadiusMeters`, `distanceMeters`). Preserve public ABI/API or serialized names where a rename would break compatibility.
3. **Readable multi-step operations.** Give distinct calculations, validation steps and side effects separate statements and meaningful intermediates; avoid deeply compressed expressions and logic chains.
4. **Use braces and sensible whitespace.** All handwritten control flow must be plainly legible; preserve language-specific style when not dictated by the owner's export.
5. **Name domain constants.** Replace unexplained physical conversions, timing/size limits, units and other important literals with named constants, without pointlessly naming `0` and `1` everywhere.
6. **Explain non-obvious constraints.** Use brief comments for thread ownership, game-specific quirks, side effects, exception cases and protocol/lifetime invariants, not commentary that restates the syntax.
7. **Keep readability in test and helper code** to the same standard as production code. A short-lived script is not exempt.

### Specific C++ requirements from the CLion XML

- Tab indentation for C++.
- 180-column right margin (not a goal to fill lines).
- PascalCase for namespaces, types and functions; camelCase for local variables and parameters; `m_PascalCase` private/protected fields when applicable; uppercase snake case for the specified constants/macros.
- Mandatory braces in `if`, `for` and `while`.
- Sorted includes and the other explicit rules in `Docs/CodeStyle.xml`.
- C++ `.h`/`.cpp` filenames and the existing one-type-per-file, namespace-mirrored project conventions.

C# and other languages should use their language-appropriate defaults for unspecified details, without sacrificing meaningful names or readability.

### Explicit example to locate and correct

Search for the hand-written one-line geographic distance routine resembling:

```csharp
public static double Distance(double la,double lo,double lb,double lob){double a=la*Math.PI/180,b=lb*Math.PI/180,x=(lb-la)*Math.PI/180,y=(lob-lo)*Math.PI/180;double h=Math.Sin(x/2)*Math.Sin(x/2)+Math.Cos(a)*Math.Cos(b)*Math.Sin(y/2)*Math.Sin(y/2);return 6371000*2*Math.Atan2(Math.Sqrt(h),Math.Sqrt(Math.Max(0,1-h)));}
```

Refactor its implementation for clarity, with well-named intermediate values and constants. Keep any externally consumed signature intact unless a compatible migration is explicitly justified. **Do not casually change floating-point operations or swap in APIs missing from the game's target .NET runtime.** Add or retain representative numerical regression tests (coincident locations, typical distances, date line, and near-pole inputs as applicable). Use this as a pattern for the entire codebase, not an isolated cosmetic fix.

### Scope exclusions

Do **not** manually modify generated C++/C# Protobuf bindings, other generated output, vendored packages, proprietary game assemblies, decompiled sources, binary assets or old evidence logs. Prefer configuring generators or formatters at their source if needed. Avoid indiscriminate folder-wide formatting. Do not rename Protobuf field numbers, RPC names, wire enums, C ABI exports, stable CMake targets or serialized fields for appearance.

## 3. Keep behavior-preserving changes distinguishable

1. Record a **before** baseline: Git revisions/status per repository, important build/test commands and currently passing checks. Do not overwrite uncommitted user work.
2. Separate mechanical formatting from readability refactors that change identifiers or expressions. Use small, reviewable commits per repository; for significant files, keep renaming and computation changes independently reviewable.
3. No gameplay, AirOps, order, gateway or protocol feature changes. Do not use this task to finish or replace Task 006.
4. Preserve task/thread ownership, native emergency automation, selected-side information restrictions, canonical Protobuf and ABI compatibility, package layout and diagnostic behavior.
5. Run unit/contract/integration/regression tests after relevant changes; for Sea Power live tests use the established **opt-in, reversible** test harness only. Do not claim a test passed if it was not executed. Avoid altering the vendor installation.
6. Introduce formatting automation (`.editorconfig`, `.clang-format`, lint hooks, etc.) only where it faithfully implements the owner's XML and `CODE_STYLE.md`; record settings or limitations. Do not impose an invented global C# rule based on the C++ XML.

## 4. Adopt the static-runtime decision without hiding a toolchain change

The cleaned `Docs/MODULE_STRUCTURE.md` states the desired Windows configuration: **vcpkg `x64-windows-static`**, with **`/MT`** in Release and **`/MTd`** in Debug. Earlier Task 005 evidence legitimately reports builds using `x64-windows-static-md` and `/MD`; that historical evidence must remain unchanged.

Audit all **active** CMake presets, vcpkg settings, scripts and package/runtime assumptions for this mismatch. Where the implementation has not yet adopted the decision, migrate active configuration to the documented `x64-windows-static`/static-CRT policy in a **separate, explicitly identified change**, not mixed invisibly into a formatting commit. Validate a fresh configure, dependency rebuild/relink as necessary, tests and runtime binary compatibility. If the switch is not feasible in the current environment, report the blocker and leave a reproducible migration plan; **do not claim it was validated merely because the docs say so**.

Retain the existing shared `Build.CMake`, `NavalCommandAddDependency`/optional `Build.Local.cmake`, configure-only presets and six-repository separation. Do not reintroduce CPM, duplicated build helpers or user-only presets.

## 5. Verification and deliverables

Create a concise report **at `Docs/Tasks/007/REPORT.md`**; store supporting command logs/manifests under `Docs/Evidence/Task007/`, not the permanent docs root.

The report must include:

- Which current docs were read, and which active paths were updated for the new layout.
- A per-repository inventory of inspected handwritten files; counts/categories of unreadable code corrected; meaningful examples of before/after names and structure.
- A separate list of remaining readability findings with specific file paths and reasons, if any. Never assert that all code is clean without a completed audit.
- Explicit exclusions for generated and third-party code.
- Tests executed and their actual results, including baseline comparison and any live-run limitations.
- Status of `x64-windows-static`/`/MT` adoption and the verified linker/runtime flags, or an honest blocker.
- Git revisions/commits for every modified repository and confirmation that no unrelated features or proprietary assets were changed.
- Documentation Git status and confirmation that new task reports/evidence are filed in their designated subfolders. Root normative docs should remain free of task progress notes.

**Acceptance criteria:** A reviewer can read handwritten code across the entire project without encountering compressed one-line methods or opaque names; existing behavior remains covered by the tests actually run; active paths honor the reorganized `Docs` repository; and no duplicate project-wide `AGENTS.md` is created outside it. The desired static CRT configuration is either verified in current builds or explicitly recorded as an outstanding blocked migration.
