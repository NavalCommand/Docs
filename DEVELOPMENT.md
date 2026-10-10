# Production, development tooling and experiments

**Authority:** Permanent engineering policy for every NavalCommandFramework repository. Read with [AGENTS.md](AGENTS.md), [ARCHITECTURE.md](ARCHITECTURE.md), and [CODE_STYLE.md](CODE_STYLE.md). This document is not a work log; numbered assignments and evidence belong under [Tasks/](Tasks/README.md) and [Evidence/](Evidence/README.md).

## Source ownership and lifecycle

Every handwritten implementation added to a repository must belong to exactly one of the following categories:

| Category | May be maintained? | Location and deployment |
|---|---|---|
| Production code | Yes; must have a stable role and runtime behavior | Production source and production package only |
| Reusable development infrastructure | Yes; stable task-independent behavior with tests and configuration | Separate developer target and package; disabled by default |
| Regression test / scenario fixture | Yes; reproduces specific safety or functional behavior | `test/`, development fixtures, or an isolated task fixture; never shipped as runtime functionality |
| Disposable experiment | Only while actively investigated; no permanent runtime contract | Isolated scratch/worktree or numbered task fixture; archive necessary source/results or delete at close |

The classification applies to source files, classes, build targets, configuration keys, deployment scripts, plugin identities, process launchers, and output artifacts—not only directories.

## Maintained code must be task-agnostic

Production interfaces, runtime configuration, Harmony/BepInEx identifiers, assembly names, logging semantics, feature flags, package names, scripts and reusable harness core must not encode the work item's task number or a one-off experiment's purpose. An environment variable such as `NC_TASKNNN_ENABLED` is appropriate only inside a narrowly scoped historical probe, not as a permanent production bootstrap. Do not rename such a flag to something generic while preserving an experiment-specific branching model; remove the coupling.

A stable harness may expose reusable actions such as `LoadScenario`, `WaitForPlayable`, `CaptureEvidence`, and an explicitly requested `ReloadMission` test. It must not steadily accumulate `TaskNNN`, game-feature-specific diagnostic branches, privileged observations or test modes. Keep specialized investigative probes independently opt-in and external to the stable harness core. Introduce a new generic harness feature only when actual reuse, safe ownership and a testable interface justify the maintenance cost.

## Production and developer separation

`SeaPower.Mod/managed/runtime/` contains the production managed integration. `SeaPower.Mod/managed/harness/` contains the optional reusable development-only C# tool. They are distinct projects and build outputs, with no reference from runtime to harness. The game-facing `SeaPower.Client` and native gateway remain separate implementations as specified by [ARCHITECTURE.md](ARCHITECTURE.md).

The production mod must start without the harness, a scenario loader, a test receipt or a task-named environment variable. The harness must start a controlled test scenario without loading the mod, and may exercise it only through an independent explicit enablement. Production settings must have stable names, validated values and documented defaults; task-specific scenario selections belong to developer configuration. Runtime logging and lifecycle handling must work without a test-run directory.

Separate build and packaging manifests must make the boundary auditable. Production packages exclude harness assemblies, diagnostic probes, test scenarios, dev-only dependencies, experimental loader aliases, or proprietary game/system assemblies. Development launchers remain opt-in, checksum-validated, reversible, and may stop **only** their owned game processes. Keep the normal game launch and vendor binaries untouched.

## Experiment workflow

1. **Plan:** State the concrete question, permitted instrumentation, expected evidence, rollback and owner of any temporary files. Prefer a disposable game fixture and existing harness primitives.
2. **Run:** Keep privileged state out of general operational observations. Label mock/simulated results separately from live game evidence. Save actual version/hash, commands, outcome and limitations.
3. **Decide:** When the investigation ends, either **promote** a verified reusable capability, **archive** the reproduction source and findings under the numbered task, or **remove** the experiment. Do not silently merge temporary branches or debug convenience into production.
4. **Verify:** Check all touched targets and deployed packages for task-specific names/flags, accidental dependencies, stale paths, old plugin IDs, unused startup toggles, test-only binaries and abandoned scaffolding. Run appropriate regression and packaging checks.
5. **Report:** In the task report, inventory what was promoted, archived, and deleted; report safety boundaries, exact test executions, blocked verification and deliberately retained exceptions.

Historical reports, frozen evidence, source snapshots and task-numbered test receipts may preserve their original names. Do **not** rewrite historical records to resemble the current architecture.

## Review and acceptance gate

For each implementation change, reviewers and coding agents must confirm:

- Production modules compile and run without the harness installed or enabled; development tooling is separately built/activated.
- No task-number/experiment-specific runtime identifiers, environment keys or branching paths enter maintained production or shared harness logic.
- The source owner and build/deployment boundary of each new file are obvious; no unused helpers or speculative abstraction layers are left behind.
- The production distribution excludes development-only code and dependencies, and a developer package has reversible install/launch/cleanup steps.
- Native game automation protections, faction-visible observations, main-thread rules, queue bounds and the no-vendor-binary-edit rule are preserved.
- Quality includes **readable** code per CODE_STYLE.md **and** correct separation of maintained code from disposable experiments.
- Tests, log/evidence paths, package manifests, and known gaps are reported accurately; a test not run is not marked passed.

The acceptance gate is an enduring rule for all future tasks, not a reason to retrofit unrelated experimental history into production code.

Managed build and distribution ownership follow [MANAGED_BUILD.md](MANAGED_BUILD.md): CMake resolves sources and orchestrates full builds, normal MSBuild projects remain independently usable after configuration, and repository-generated files stay under `o/`. Production and development packages have separate CMake targets and stages; neither staging nor an IDE build activates a loader.
