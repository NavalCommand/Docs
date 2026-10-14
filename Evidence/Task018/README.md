# Task 018 runtime evidence

See [the investigation report](../../Tasks/018/REPORT.md) for interpretation and limits.

| Directory | Scenario and observer | Result |
|---|---|---|
| `PilotAircraft-Taskforce1/` | F-14 vs. MiG, Taskforce1 | Captured exact platform class and separate missile, fuel-tank and pilot contact rows. The scenario developed into combat despite `WeaponStatus=Hold`; only its early identification measurements are used for classification progression. |
| `Surface-Taskforce2/` | Perry vs. Krivak, Taskforce2 | 12-second capture completed before a native Noesis reflection crash report; closed normally after capture. |
| `Surface-Taskforce1/` | Same surface scenario, Taskforce1 | 12-second capture completed; reverse contact classes and relationships matched. |

Each run directory retains its request, readiness, launch, fixture hash, harness/package manifest, raw loader log, result, and cleanup receipt. `fixture-build.log` records the separate task-scoped probe build. The fixture source and local scenario files are in `Docs/Tasks/018/Fixture/`.
