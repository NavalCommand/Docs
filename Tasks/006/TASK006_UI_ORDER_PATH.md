# Native movement command path

Inspected installation: Sea Power **0.8.3-261001**, Unity 6000.0.67f1. All native members below are in `Sea Power_Data/Managed/Seapower-Scripts.dll`, SHA256 `0FE6AAF2A6D9982E7E2EB41FD6035CB97C149773202C90821D71C00A9FDEB01A`. Inspection used ILSpy against that installation; no proprietary source or binary is committed. Evidence and live receipts are in [Task006 evidence](../../Evidence/Task006/).

## UI dispatch and exact mutation

`SeapowerUI.ViewModels.UnitSelectedState.MouseDown` dispatches an owned, order-accepting right-click to `HandleUnit`. Its map branch checks left/right Shift. The ordinary branch redirects to a formation leader where applicable, checks control, cancels current orders/refuelling/patrol/formation effects, can call `Aircraft.CeaseFire` when weapons are free, calls `ObjectBase.RemoveWaypoints`, then lower-case `ObjectBase.setWaypointTask`. It also records `CommandPosition`.

`ObjectBase.RemoveWaypoints` resets native waypoint bookkeeping and calls `UserRootNode.ClearTasks`. **This clears every child task and disposes its viewmodel**, not just navigation. Calling it indiscriminately would destroy unrelated tasks. The adapter therefore requires an exact one-to-one correspondence between user-root `ActionNode` children and task viewmodels, with every task an ordinary `GoToWaypointTask`, before replacement. Selected non-navigation tasks, hidden graph children, special route options, formations, active other orders, patrol and alignment contexts are rejected.

The Shift branch inspects `GetLastWaypoint`, rejects loop routes, preserves height according to existing task selection, and calls `setWaypointTask`. It can itself clear the route when `_isCurrentWaypointReachable` is false. `UserRootNode.AddTask(task)` appends, whereas `AddTask(task, after)` inserts after its argument. Both lower-case `setWaypointTask` and upper-case `SetWaypointTask` / `setWaypointTaskParameters` insert after `_userRoot.SelectedTask` when one exists. Thus **Shift-click is not universally a tail append** if a user selected an earlier waypoint in the GUI.

The external append intent explicitly selects the existing tail before `SetWaypointTask`, guaranteeing append after the whole route. This is equivalent to native Shift-click with the tail selected in a reachable, ordinary route. The API rejects the native unreachable-route fallback instead of unexpectedly clearing an append request.

`GoToWaypointTask.OnExecute` changes speed only when `_setSpeed.value` is true. `setWaypointTaskParameters` treats `_setTelegraph > -2` as an explicit speed command. Explicit replace/append use **-2**, preserving native speed. The existing legacy request retains its established telegraph 2 behavior. No `CeaseFire`, global AI disable, forced state transition or formation propagation is used.

## Equivalence and evidence

| External operation | Validated native operation | Evidence and limits |
|---|---|---|
| Legacy omitted mode | Existing idle-only `SetWaypointTask` path | Live first-waypoint movement still passes; never silently becomes replacement |
| Replace route | `RemoveWaypoints` then `SetWaypointTask` on a proven pure navigation root | Live moving vessel: one route replaced; three queued points replaced by exactly one |
| Append waypoint | Tail selection then `SetWaypointTask`; previous task references remain in order | Live native root sequence B → C → D retained and independently queried |
| Protected aircraft | Reject before native mutation | `AircraftStates.AdvancedMissileEvasion` observed by isolated fixture; legacy, replace and append rejected; protected state and original route remain in subsequent observation |
| Non-navigation / formation | Conservative rejection | Typed/source guard and provider regressions; no live non-navigation or formation fixture was executed |

The copied native task `WaypointPosition` and `wpName`, exact task reference sequence, child/model correspondence and independent `WorldSnapshot.planned_route` queries establish route changes. Displacement alone is insufficient. New append receipts remain `ROUTE_MUTATED` while a prior route head is active; displacement then cannot be attributed to executing the queued append.

No live GUI click was performed. Static UI-path inspection plus direct native mutations through the external gateway establish the stated equivalence **within the isolated ordinary surface-navigation subset**. Broader aircraft routing, UI side effects and formation semantics are deliberately unsupported. No arrival claim is made.

## Facility callsites

`SeaPower.FlightDeck.CanLaunchVehicle` checks accountable loadout/ammunition resources. `SeaPower.FlightDeck.createLaunchTask` debits resources, updates availability and delegates reservation to `SeaPower.FlightDeckTask.CreatePendingLaunchTask`; its checks alone are insufficient authorization. `SeaPower.PendingLaunchTask` manages crew/deck/preparation states. `SeaPower.HandlePendingLaunchTask.onUpdate` calls `SeaPower.FlightDeck.launchVehicle` for execution. `PendingLaunchTask.Launched` indicates object spawning; `SeaPower.PlaneTakeOff.onEnter` / `onUpdate` continue native taxi/takeoff. Airborne requires the aircraft's `_isInFlight` and cleared `_flightDeckState`, and is reported separately from the spawn callback.
