# Work history and implementation records

This directory contains historical work specifications, reports and reproductions. They are valuable evidence and references for further investigation but do **not** override the permanent requirements at the `Docs/` root.

| Work item | Scope and available records |
|---|---|
| [001](001/) | Static Sea Power reverse-engineering request and [technical findings](001/SEAPOWER_FINDINGS.md) |
| [002](002/) | Runtime validation request, [runtime findings](002/SEAPOWER_RUNTIME_FINDINGS.md), [reproduction](002/TASK002_REPRODUCTION.md), [automatic launcher](002/AUTOMATED_LAUNCH.md) |
| [003](003/) | Air-facility investigation request, [AirOps findings](003/SEAPOWER_AIROPS_FINDINGS.md), [reproduction](003/AIROPS_REPRODUCTION.md) |
| [004](004/) | First end-to-end gateway implementation assignment, [results](004/TASK004_IMPLEMENTATION.md), [reproduction](004/TASK004_REPRODUCTION.md) |
| [005](005/) | [Completed module/build restructuring report](005/TASK005_IMPLEMENTATION.md); its originally recorded compiler/runtime settings are historical, not the desired current `x64-windows-static` policy |
| [006](006/) | Repeatable movement and facility-launch assignment (not evidence of completion) |
| [007](007/007-code-readability-and-docs-adoption.md) | Repository-wide readability and documentation adoption; [implementation report](007/REPORT.md) |

Run-specific evidence remains in [../Evidence/](../Evidence/README.md) under its original directory identifiers. Some archived commands or inline `Docs/...` paths reflect the former root-level document layout; use this index when locating moved reports.

**Archive completeness note:** the archived AirOps reproduction guide links to `../Experiments/Task002/AirFacilities.ini`, which was not present in the supplied `Docs.zip`. That fixture has not been fabricated or silently replaced. Recover it from the actual source checkout if a reproduction is needed.
