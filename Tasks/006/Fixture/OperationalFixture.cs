using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Threading;
using BepInEx;
using HarmonyLib;
using SeaPower;
using UnityEngine;

// Isolated regression fixture. Not compiled into runtime or harness packages.
[BepInPlugin("dev.navalcommand.fixture.operational", "Operational safety fixture", "1.0.0")]
public sealed class OperationalFixture : BaseUnityPlugin
{
    static OperationalFixture instance;
    readonly ConcurrentQueue<string> lines = new();
    volatile bool stop;
    Harmony harmony;
    FlightDeck deck;
    float ammoBefore;
    bool restored;
    float restoreAtWallSeconds;
    double nextSampleSeconds;
    int lineCount;
    string lastAircraftStates;

    void Awake()
    {
        if (System.Environment.GetEnvironmentVariable("NC_FIXTURE_ENABLED") != "1")
        {
            return;
        }
        string directory = System.Environment.GetEnvironmentVariable("NC_HARNESS_RUN_DIRECTORY");
        if (string.IsNullOrWhiteSpace(directory) || !Path.IsPathRooted(directory))
        {
            return;
        }
        instance = this;
        new Thread(() => Write(Path.Combine(directory, "native-fixture.log"))) { IsBackground = true }.Start();
        harmony = new Harmony("dev.navalcommand.fixture.operational");
        harmony.Patch(typeof(JobPerformer).GetMethod(nameof(JobPerformer.CompleteFixedUpdateJobs)),
            postfix: new HarmonyMethod(typeof(OperationalFixture).GetMethod(nameof(AfterJobs))));
    }

    public static void AfterJobs() => instance?.Sample();

    void Emit(string text)
    {
        if (++lineCount <= 512)
        {
            lines.Enqueue(DateTime.UtcNow.ToString("o") + " " + text);
        }
    }

    void Sample()
    {
        if (Globals.mainThread != Thread.CurrentThread || !ObjectCalculationsJobScheduler.HasPreviousJobCompleted
            || !CollisionAvoidanceJobScheduler.HasPreviousJobCompleted || Globals._playerTaskforce == null
            || !Singleton<GameMain>.InstanceExists() || !Singleton<GameMain>.Instance._initialized || GameTime.missionElapsedTime < 2)
        {
            return;
        }
        string aircraftStates = string.Join(";", Globals._playerTaskforce._taskforceUnits.OfType<Aircraft>().Take(32).Select(unit =>
            "uid=" + unit.UniqueID + " state=" + unit.StateMachine?.CurrentState?.GetType().Name
            + " flightDeckState=" + unit._flightDeckState + " inFlight=" + unit._isInFlight));
        if (aircraftStates != lastAircraftStates)
        {
            lastAircraftStates = aircraftStates;
            Emit("native-aircraft-states seconds=" + GameTime.missionElapsedTime + " " + aircraftStates);
        }
        if (deck == null)
        {
            var facility = Globals._playerTaskforce._taskforceUnits.OfType<LandUnit>().FirstOrDefault(unit => unit._obp?._flightDeck != null);
            if (facility == null)
            {
                return;
            }
            deck = facility._obp._flightDeck;
            ammoBefore = deck._currentAmmo;
            // Bounded shortage injection in a disposable own airfield, restored after
            // ten wall seconds. No inventory, task, preparation or aircraft mutation.
            deck._currentAmmo = -1;
            restoreAtWallSeconds = Time.realtimeSinceStartup + 10;
            Emit("resource-injection ammoBefore=" + ammoBefore + " stock=" + deck.TotalVehiclesOnBoard() + " tasks=" + deck.FlightDeckTasks.Count);
        }
        if (!restored && Time.realtimeSinceStartup >= restoreAtWallSeconds)
        {
            deck._currentAmmo = ammoBefore;
            restored = true;
            GameTime.SetMaxTimeCompression();
            Emit("resources-restored stock=" + deck.TotalVehiclesOnBoard() + " tasks=" + deck.FlightDeckTasks.Count
                + " compression=" + GameTime.TimeCompression + " native-maximum native-timings-unchanged");
        }
        if (GameTime.missionElapsedTime >= nextSampleSeconds)
        {
            nextSampleSeconds = GameTime.missionElapsedTime + 30;
            Emit("facility seconds=" + GameTime.missionElapsedTime + " ammo=" + deck._currentAmmo + " unreserved=" + deck.TotalVehiclesOnBoard()
                + " physicalOnboard=" + deck.AircraftCurrentlyOnBoard + " tasks=" + string.Join(",", deck.FlightDeckTasks.Select(task =>
                    task.GetType().Name + ":" + task._stateMachine?.CurrentState?.GetType().Name + ":work=" + task._duration)));
            foreach (var unit in Globals._playerTaskforce._taskforceUnits.OfType<Aircraft>())
            {
                Emit("own-aircraft uid=" + unit.UniqueID + " state=" + unit.StateMachine?.CurrentState?.GetType().Name
                    + " flightDeckState=" + unit._flightDeckState + " inFlight=" + unit._isInFlight);
            }
        }
    }

    void Write(string path)
    {
        using var writer = new StreamWriter(path);
        while (!stop || !lines.IsEmpty)
        {
            if (lines.TryDequeue(out string line))
            {
                writer.WriteLine(line);
                writer.Flush();
            }
            else
            {
                Thread.Sleep(50);
            }
        }
    }

    void OnDestroy()
    {
        harmony?.UnpatchSelf();
        if (deck != null && !restored)
        {
            deck._currentAmmo = ammoBefore;
        }
        stop = true;
        instance = null;
    }
}
