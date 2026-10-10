# Facility launch lifecycle stabilization receipts

These receipts correct Task006's pushed implementation; no new numbered task or protocol is introduced. Original Task006 receipts remain unchanged. See the [Stabilization section](../../../Tasks/006/TASK006_IMPLEMENTATION.md).

## Results

| Check | Actual outcome / evidence |
|---|---|
| Issue A: transient task/registration gaps | Resolved by actual `LaunchLifecycle` source-linked tests: `first_poll_spawn_gap_preserves_known_acceptance`, `missing_task_keeps_tracking`, `callback_before_registration_no_invented_id`, registration grace and absolute deadlines |
| Issue B: uncertain native acceptance owner | Resolved by source-tested creation helper plus provider tests: unverified reservation keeps token; creation exception with no task creates no subscription; exception after insertion recovers a correlated task; verification exception retains existing owner |
| Normal progression | Actual lifecycle tests and final live native launch: preparing → ordinary grounded spawn → airborne |
| Cancellation / destruction | Deterministic lifecycle facts finalize and release ownership; no live destruction/cancellation injection |
| Duplicate / old session / old handle | Provider fakes retain uncertain/terminal receipts without another reservation; reset clears ownership and rejects previous session/handle |
| Active / uncertain reset | Actual idempotent lifecycle disposal and provider facade-reset tests; no live mid-launch mission reset in this pass |
| Leak / deadline checks | 256 repeated absent-spawn failures release all cleanup actions; terminal state stable; pending/grounded tracking expires without invented completion |
| Publication exceptions | Backpressure remains visible; active owner survives publication failure, terminal owner removed before publication, duplicate returns retained terminal receipt |
| Managed builds/tests | Clean/direct production builds and Debug/Release provider tests pass. `provider-Release.log`, `provider-Debug.log`, direct/build/package logs. Formatter verification passes with retained workspace-loading warnings |
| Native compatibility | All six Release CTest suites pass. Debug Domain.Api, Gateway.Api, Gateway.Grpc.Api and provider pass; Gateway.Grpc and SeaPower.Mod **exit 8 after passing assertions with known post-assertion SegFault**. See `ctest-results.json` and per-repository logs. Final mod Release CTest passes both native host and runtime/harness tests |
| Packages | `packages-Debug.json` and `packages-Release.json`: every manifest hash and exact runtime/harness DLL allowlist verified; production defaults disabled; no fixture or game DLLs shipped |
| Safety/output | `safety-audit.json`: 233 vendor hashes unchanged, original Doorstop hash restored, no owned processes/deployments left, no generated binaries/assets/cache outside `o/`, owner XML unchanged |

Tests exercise the production lifecycle/creation helper directly without Unity objects; native integration wrappers remain SDK-backed. Provider/fake cases are explicitly simulated. The native active/delayed task correlation filter and subscription attachment path are source-reviewed; exceptions, cancellation and registration delays were not deliberately injected into the live game.

## Final live integration

[live-launch-confirmed/cli.log](live-launch-confirmed/cli.log) reports `FACILITY_LAUNCH_PASS`, CLI exit 0. Session `b8ed084afbbb41fa870b9fe8cc287c2a`, request `launch-17916541124262829`; native acceptance/preparation at 399.406513 mission seconds, ordinary own spawn at 1307.749237, airborne at 1414.853960. Correlated regular unit ID is `7cc1e9baf33e455685047a1fefd1b9c3`. Native fixture records taxi/takeoff and `_isInFlight` false→true; CLI's success requires observing a regular grounded unit before airborne. Runtime, harness and explicit fixture hash manifests identify the actually deployed artifacts. Negative resource/type/squadron/loadout/count/handle/view requests and duplicate launch also pass.

The isolated archived fixture calls `SeaPower.GameTime.SetMaxTimeCompression` after restoring the ten-wall-second resource-shortage injection. Native maximum **200×** is recorded; ready-up/taxi/takeoff durations are not skipped or edited. `live-launch` and `live-launch-final` are earlier successful stabilization runs; the confirmed run includes the final publication-ownership ordering. Cleanup receipts restore exact original Doorstop bytes and remove only owned deployments/processes. Known loader UnityLogWriter warnings remain visible.

## Native ordering and limitations

Installation remains Sea Power 0.8.3-261001, Seapower-Scripts.dll SHA256 `0FE6AAF2A6D9982E7E2EB41FD6035CB97C149773202C90821D71C00A9FDEB01A`.

Static inspection of `SeaPower.HandlePendingLaunchTask.onUpdate` verifies it calls `FlightDeck.launchVehicle`, invokes `Launched`, then decrements LaunchCount. `PendingLaunchTask` forwards that event. `FlightDeck.getObjectToLaunch` requests asynchronous aircraft loading (`ObjectsManager.createAircraft(...loadAsync:true)`); task add/removal is separately managed by `FlightDeck.handleFlightDeckTasks` and delayed lists. The callback is not a provider-registration barrier. `FlightDeck.createLaunchTask` debits resources before task insertion/return, so exceptions cannot imply no mutation. The actual callback still asserts `Globals.mainThread == Thread.CurrentThread` before recording the native object; successful correlated live progress exercised that guard. All native reads, timer decisions, subscription attachment/removal and dispatch run on the existing verified game thread, without disk/network/worker waits. No proprietary source or binaries are committed.

Visibility grace is 30 monotonic wall seconds; total tracking bound is 1,800 wall seconds. A long pause/contention can produce terminal unknown while native automation continues. Uncorrelatable creation exceptions remain genuinely unknown; no retry or speculative resource rollback is attempted. Known Debug shutdown failures are unchanged. Prior Task006 live formation/non-navigation gaps remain outside this targeted correction.

Archived audit runners retain local execution paths as evidence only. Production build configuration and dependency/output architecture are unchanged. Separate local source and Docs commits are recorded; nothing is pushed.
