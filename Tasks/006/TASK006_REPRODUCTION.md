# Build and live reproduction

Run from each actual repository root in a VS 2026 x64 developer environment. Use ignored `Build.Local.cmake` registrations and the standard `FETCHCONTENT_SOURCE_DIR_NCBUILD` bootstrap as documented in [LOCAL_SOURCES](../../LOCAL_SOURCES.md). Native external dependencies use `x64-windows-static`. Generated managed context and outputs remain under each repository's `o/`.

```powershell
cmake --preset windows-msvc-release --fresh -DFETCHCONTENT_SOURCE_DIR_NCBUILD=<Build.CMake-checkout>
cmake --build o/windows-msvc-release --parallel 8
ctest --test-dir o/windows-msvc-release --output-on-failure
```

Repeat in Domain.Api, Gateway.Api, Gateway.Grpc.Api, Gateway.Grpc, SeaPower.Client and SeaPower.Mod, then with windows-msvc-debug. After configuration, ordinary `dotnet build managed/SeaPower.Client.csproj -c Release` and mod runtime/harness project builds consume generated context automatically. Run provider tests with `dotnet run --project test/managed/ProviderTests.csproj -c Release`. Gateway.Api CTest includes schema lock and C#→C++→C# conformance. Exact executed clean verification script, arguments and outcomes are archived under [evidence](../../Evidence/Task006/).

In SeaPower.Mod, build `package-runtime` and `package-harness` targets. Distributions remain separate under `o/`; neither contains the operational fixture. Normal runtime config remains disabled by default. Existing Debug post-assertion gateway/host exit failures are unresolved failures, not successful CTest results.

## External live route test

The already approved development loader must be installed. This opts into reversible game-folder deployment; use fresh evidence directories and disposable own-side scenarios. Substitute absolute paths below as command arguments, not committed build configuration.

```powershell
./scripts/Start-Live.ps1 -Mode Combined -GameRoot <game-root> `
    -Scenario ./managed/harness/scenarios/MinimalCombat.ini `
    -OutputDirectory <fresh-absolute-evidence-directory> `
    -Cli <Gateway.Grpc.Api-checkout>/o/windows-msvc-release/app/naval-cli.exe `
    -ClientArguments '--movement-smoke' -ApproveLoaderChanges
```

The CLI uses public RPCs: legacy waypoint establishes movement, replacement B, append C/D, replacement E, reconnect duplicate and negative requests. It checks native sequence receipts against an independently queried route and emits `MOVEMENT_ROUTE_PASS`. `--smoke` still tests the legacy meaning. `-Blocked` with AircraftThreat.ini checks all three modes during natural missile evasion. `-ReloadTest` with MinimalCombat checks old session/handles after harness-controlled reload.

## Facility launch

Use the same launcher with AirFacilities.ini and `-ClientArguments '--launch-smoke'`. The CLI chooses an advertised own-stock option, rejects invalid count/class/squadron/loadout/entity/view requests, submits one launch and duplicate, verifies one reservation, then polls for a regular grounded unit and airborne progress (bounded 1,200 seconds). Native preparation may take about 900 mission seconds before taxi/takeoff. Acceptance/spawn alone does not pass.

For resource-shortage regression and direct native state evidence, build the isolated archived fixture:

```powershell
dotnet build <Docs-checkout>/Tasks/006/Fixture/OperationalFixture.csproj -c Release `
    '-p:GameManaged=<game-root>/Sea Power_Data/Managed'
```

Pass `Docs/o/managed/OperationalFixture/bin/Release/netstandard2.1/OperationalFixture.dll` via explicit `-FixtureAssembly <absolute-dll>`. The launcher stages only that DLL in a new temporary fixture directory, sets child-only opt-in, archives its SHA256, and removes deployment afterward. The fixture injects a ten-wall-second ammunition shortage, restores resources, then selects native compression setting 5. No ready-up duration is changed. The CLI submits a *new* request after restoration; it never automatically replays the rejected ID. Without this option there is no injection or compression change. Protected-aircraft runs only sample own native state without airfield mutation.

Inspect `result.json`, `cleanup.json`, package/fixture manifests, owned plugin-folder absence and installation integrity; CLI success alone does not prove rollback. The launcher stops owned processes and restores exact original Doorstop bytes in `finally`. Existing loader warnings are retained. The first launch receipt also records a successful normal-time launch without this fixture or compression.
