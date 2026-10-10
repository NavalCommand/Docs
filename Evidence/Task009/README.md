# Task 009 evidence

- `before-inventory.json`, `before-package-inventory.json`: actual checkout revisions, pending owner edits, source/package baseline and prior startup coupling.
- `production-build.log`, `runtime-clean.log`, `runtime-build.log`, `native-configure.log`, `native-clean-build.log`, `native-compile-commands.txt`: managed/native build receipts and static CRT/compiler flags. External dependencies reused the existing pinned source-built static prefix; no third-party dependency rebuild is claimed.
- `harness-clean.log`, `harness-build.log`: independent harness build/stage.
- `release-ctest.log`, `managed-and-abi-tests.log`, `runtime-tests.log`: native ABI and 27 focused managed checks (the standalone run log contains 25 checks without the two native fixture DLL arguments).
- `gateway-release-tests.log`, `provider-tests.log`, `schema-tests.log`, `cross-language.log`, `*-payload.bin`: existing module regressions and canonical cross-language/schema compatibility.
- `mock/`: independent mock-host/CLI smoke.
- `harness-only/`, `harness-independent-final/`: separate harness readiness and cleanup, with the final run suspending the legacy helper.
- `combined-smoke/`, `protected-native/`, `reload/`: explicit combined runs and existing live regression outcomes.
- `production-only/`: no harness installed; normal game-menu mission selection screenshots, external CLI smoke and normal-close shutdown log.
- `disabled/`: independent production plugin remains inactive when disabled.
- `missing-native/`, `unwritable-log/`: explicit live startup failures with owned-process/loader cleanup.
- `debug-host-known-issue.log`, `debug-gateway-known-issue.log`: current Debug reruns passed; earlier recorded shutdown issue remains unresolved, without suppression or a claimed fix.
- `production-package-inventory.json`, `harness-package-inventory.json`, `boundary-audit.json`, `powershell-parse.json`, `after-inventory.json`: final ownership/build/package/source audits.
- `vendor-before.json`, `vendor-after.json`, `installation-before.json`, `installation-after.json`, `integrity-and-cleanup.json`: vendor hashes, exact original helper/Doorstop rollback and no leaked deployments/processes.

`Run-LiveChecks.ps1` and `OwnedWindowFixture.ps1` are narrowly scoped acceptance fixtures, not maintained runtime/harness features. The former suspends the pre-existing legacy helper with an exact-byte rollback and optionally stages a disposable mission for normal UI selection. The latter operated only the owned game window for that menu-selection acceptance check. Neither is shipped or activated normally. Original one-off diagnostic/AirOps sources are archived under Tasks/009/ArchivedProbes and are never compiled by the new projects.

Run outputs are retained as observed, including expected errors and forced versus normal cleanup. See Tasks/009/009-implementation.md for commands, interpretation and limits.
