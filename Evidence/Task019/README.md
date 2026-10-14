# Task 019 interactive identification run

See the [runtime findings](../../Tasks/019/IDENTIFICATION_RUNTIME_FINDINGS.md) for interpretation, the owner's manually reported UI sequence, limits, and follow-up questions.

This directory retains non-proprietary run evidence from Sea Power 0.8.3 Build 261001:

- `task019-native-state.jsonl`: raw Taskforce1 plotting-table recorder output. It contains observer wrapper values and selected UI view-model properties; it does not contain target `BaseObject` data or global entity scans. The report explains the unavailable observer-ECS and UI-line captures.
- `fixture-build.json`: task fixture build result, hashes, configuration, and warnings. The source is in `../../Tasks/019/Fixture/`.
- `preflight.json`, `fixture.json`, and `launch.json`: inspected game/assembly/scenario hashes, fixture identity, Harness mode, and runtime-disabled launch receipt.
- `ready.json`, `result.json`, and `cleanup.json`: readiness, normal run completion, and reversible deployment cleanup receipts.
- `harness.log` and `loader.log`: harness lifecycle and loader/fixture startup output.

The evidence excludes proprietary game assemblies, decompiled vendor source, and built fixture binaries. The recorded class keys are retained as observed output and are subject to the provenance caveats in the report.
