# Task 020 evidence

This directory contains non-proprietary investigation and fixture-build evidence for [the findings report](../../Tasks/020/OBSERVER_PRESENTATION_FINDINGS.md).

- `instrumentation-prerequisites.json` records the installed assembly metadata that explains the Task 019 failure and confirms each typed ECS prerequisite.
- `static-trace.json` records the inspected method paths and palette values without copying vendor IL or source.
- `fixture-build.json` records the Task 020 fixture source/project hashes, successful offline build, and the two existing game-reference version-conflict warnings.
- `runtime-status.json` records the authorized Harness-only launch, the disabled production runtime, and the successful loader/plugin cleanup.
- `runtime-contact-summary.json` summarizes the 54-record selected-side contact capture, including candidate flags, detected-side linkage, sensor sources, and track freshness transitions.
- `contact-7001-observer-capture.jsonl` contains the instrumentation-prerequisite record and all sampled records for contact 7001 from that run. The fixture did not record the native class key.
- The recorder source is retained in `../../Tasks/020/Fixture/`. The compiled DLL is kept under the ignored `SeaPower.Mod/o/` build directory and is not committed.

The evidence includes no proprietary game assembly, decompiled vendor source, or compiled fixture binary. The game assembly is identified only by version/build and SHA-256. The owner authorized one short run; it used Harness plus the Task 020 fixture, with the production runtime disabled.
