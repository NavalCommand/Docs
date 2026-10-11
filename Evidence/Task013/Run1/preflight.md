# Task 013 Run 1 preflight

Date: 2026-10-11 (Europe/Berlin)

- Game assembly: `Seapower-Scripts.dll` SHA-256 `0FE6AAF2A6D9982E7E2EB41FD6035CB97C149773202C90821D71C00A9FDEB01A`.
- The exact prior diagnostic artifact `BepInEx/plugins/Task012EngagementProbe.dll` was absent. No removal was needed; unrelated installation files were left in place.
- `doorstop_config.ini` was `enabled = false`, SHA-256 `3B5FBCF2C104F967F63687A0FD240CB79A5253D08F4338A358EE796C17B5AA0A`.
- No Sea Power process or existing `NavalCommand.Runtime`, `NavalCommand.Harness` or `NavalCommand.Fixture` plugin directory was present.
- The bounded run used `Start-Live.ps1 -Mode Harness`, the no-fire Task 012 fixture, and the temporary probe assembly SHA-256 `A1D8706B793063ACB36E4D87CBADC4B274DA8D2B89CA916D732B337CAD6EA427`.
- `cleanup.json` confirms the original doorstop hash was restored and the runner removed its owned plugin directories/process.
