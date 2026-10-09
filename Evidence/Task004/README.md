# Task 004 evidence index

All files here are bounded own test output/notes, not game binaries or decompiled source. Final source revisions are in source-revisions.json. Builds were fresh clones with shared source-built external dependencies; build-*.log retains relevant compiler, fetch, test and managed-build excerpts. conformance.log is the actual cross-language/schema test output. dependencies.log records relevant original source-build output. hosting-sources.json compares the generic gateway implementation used by both hosts with CRLF normalized to LF for Git checkout differences.

| Directory/file | What it proves |
|---|---|
| waypoint | Fresh-clone composed package and independently built CLI, real embedded native ABI/gRPC, one waypoint, rejection probes, retained reconnect result, 5.019104786 m observed displacement |
| protected | AircraftThreat native evasion, positive selected-side contact estimate, game-thread protected_native_action rejection |
| reload | Native scene unload, new session/handle, old-session and old-handle rejection through still-running host |
| mock | Fresh-clone generic server with mock provider and external CLI; synthetic movement explicitly labelled |
| package-manifest.json | Final tested staged DLL/config identities; ZIP includes third-party license notices |
| vendor-hashes.json | Exact before/after checks for all 233 vendor executable/managed files |
| installation-check.json | Zero vendor changes, original own helper restored, Doorstop inactive, no test game process remaining |

Final waypoint session: `a48dae5ab0c6444eb03c6ef9663eff7f`, request `smoke-17915694937862633`, unit handle `024e2d5b132f445f9a3ecb4791cd93db`. Task created on thread 1 at operational 6.01474959077314 s, movement at 11.8566416501999 s. Native host SHA-256 `58C45FD5DC8EB68261C267F98445E63C162187D1A9B128C1B4E30EE06BCA2141`. Detailed wall-clock times are in gateway.log and launch/ready records. No arrival or exclusive causal attribution claim.

Protected run session `2563f7433bde478e81743ad32d644586`, request `smoke-17915691106050161`, rejection operational 12.63965884456411 s. Its native host was the original checkout build; both use the same committed gateway/provider sources. Reload run old session `caecac4c4d604ab19f24870ae6107d4d` changed to `7de9aad17f014a69be3ebeace0f0e5d0`.

Earlier failed experimental runs remain in temporary build directories, summarized honestly in the implementation report: Unity component/external assembly resolution, then NuGet/Unity Span identity mismatch; no successful RPC claim for those runs. Final clean-clone relocation first failed on backslash Git URLs and succeeded with forward slashes. Full verbose dependency/compiler/game logs are not copied here. Current tests close owned game processes forcibly; these files do not prove graceful game-exit native shutdown. Facility observations do not prove new-bridge launch/spawn.
