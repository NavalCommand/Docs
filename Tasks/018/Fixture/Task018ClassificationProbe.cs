using System;
using System.Collections.Generic;
using System.Threading;
using BepInEx;
using SeaPower;
using UnityEngine;

namespace NavalCommand.Task018;

[BepInPlugin("dev.navalcommand.task018.classification-probe", "Task 018 Classification Probe", "0.1.0")]
public sealed class Task018ClassificationProbe : BaseUnityPlugin
{
    private const float CaptureIntervalSeconds = 0.25f;
    private const float SummaryIntervalSeconds = 2.0f;
    private const float CaptureDurationSeconds = 12.0f;
    private const float StartupTimeoutSeconds = 180.0f;

    private readonly Dictionary<int, string> previousStates = new Dictionary<int, string>();
    private float enabledAtRealtimeSeconds;
    private float captureStartedAtRealtimeSeconds;
    private float nextCaptureAtRealtimeSeconds;
    private float nextSummaryAtRealtimeSeconds;
    private string observerSide;
    private bool enabledForRun;
    private bool captureStarted;
    private bool finished;

    private void Awake()
    {
        enabledForRun = System.Environment.GetEnvironmentVariable("NC_FIXTURE_ENABLED") == "1";
        observerSide = System.Environment.GetEnvironmentVariable("NC_HARNESS_SIDE") ?? "unknown";
        enabledAtRealtimeSeconds = Time.realtimeSinceStartup;

        if (enabledForRun)
        {
            Logger.LogInfo(
                "TASK018 fixture enabled; it reads only the selected observer's plotting-table wrapper fields.");
        }
    }

    private void Update()
    {
        if (!enabledForRun || finished || Globals.mainThread != Thread.CurrentThread)
        {
            return;
        }

        float nowRealtimeSeconds = Time.realtimeSinceStartup;
        PlottingTable plottingTable = Globals._playerTaskforce?.PlottingTable;
        if (plottingTable == null)
        {
            if (nowRealtimeSeconds - enabledAtRealtimeSeconds >= StartupTimeoutSeconds)
            {
                Logger.LogInfo("TASK018 failed: selected-side plotting table did not become available.");
                finished = true;
            }

            return;
        }

        if (!captureStarted)
        {
            captureStarted = true;
            captureStartedAtRealtimeSeconds = nowRealtimeSeconds;
            nextSummaryAtRealtimeSeconds = nowRealtimeSeconds;
            Logger.LogInfo(
                "TASK018 capture-start observer=" + observerSide
                + " missionTimeSeconds=" + GameTime.missionElapsedTime.ToString("F1"));
        }

        if (nowRealtimeSeconds - captureStartedAtRealtimeSeconds >= CaptureDurationSeconds)
        {
            Logger.LogInfo(
                "TASK018 capture-finished observer=" + observerSide
                + " durationSeconds=" + CaptureDurationSeconds.ToString("F1")
                + " lastMissionTimeSeconds=" + GameTime.missionElapsedTime.ToString("F1"));
            finished = true;
            return;
        }

        if (nowRealtimeSeconds < nextCaptureAtRealtimeSeconds)
        {
            return;
        }

        nextCaptureAtRealtimeSeconds = nowRealtimeSeconds + CaptureIntervalSeconds;
        CaptureTrackStates(plottingTable);

        if (nowRealtimeSeconds >= nextSummaryAtRealtimeSeconds)
        {
            LogSummary(plottingTable);
            nextSummaryAtRealtimeSeconds = nowRealtimeSeconds + SummaryIntervalSeconds;
        }
    }

    private void CaptureTrackStates(PlottingTable plottingTable)
    {
        HashSet<int> currentTrackIds = new HashSet<int>();

        foreach (Vehicle track in plottingTable.Vehicles)
        {
            if (track == null)
            {
                continue;
            }

            currentTrackIds.Add(track.Id);
            string state = DescribeObserverState(track);
            if (!previousStates.TryGetValue(track.Id, out string previousState) || previousState != state)
            {
                Logger.LogInfo(
                    "TASK018 track-state observer=" + observerSide
                    + " trackId=" + track.Id
                    + " missionTimeSeconds=" + GameTime.missionElapsedTime.ToString("F1")
                    + " " + state);
                previousStates[track.Id] = state;
            }
        }

        List<int> disappearedTrackIds = new List<int>();
        foreach (int priorTrackId in previousStates.Keys)
        {
            if (!currentTrackIds.Contains(priorTrackId))
            {
                disappearedTrackIds.Add(priorTrackId);
            }
        }

        foreach (int disappearedTrackId in disappearedTrackIds)
        {
            Logger.LogInfo(
                "TASK018 track-absent observer=" + observerSide
                + " trackId=" + disappearedTrackId
                + " missionTimeSeconds=" + GameTime.missionElapsedTime.ToString("F1"));
            previousStates.Remove(disappearedTrackId);
        }
    }

    private static string DescribeObserverState(Vehicle track)
    {
        bool classPresent = track.Class.HasValue;
        string classValue = classPresent ? track.Class.Value.Value : "unknown";
        bool classValueNonEmpty = classPresent && !string.IsNullOrEmpty(classValue);
        bool sidePresent = track.Side.HasValue;
        string relationship = "unknown";

        if (sidePresent && track.Side.Value.Value != null && track.ReportingSide != null)
        {
            relationship = track.ReportingSide.RelationshipTo(track.Side.Value.Value).ToString();
        }

        return "identified=" + track.Identified.Value
            + " classFieldPresent=" + classPresent
            + " classValueNonEmpty=" + classValueNonEmpty
            + " class=\"" + classValue + "\""
            + " sideKnown=" + sidePresent
            + " relationship=" + relationship
            + " liveTrack=" + track.HasLiveTrack
            + " positionEstimatePresent=" + track.PositionEstimate.HasValue;
    }

    private void LogSummary(PlottingTable plottingTable)
    {
        int identifiedCount = 0;
        int classFieldPresentCount = 0;
        int classValueCount = 0;
        int sideKnownCount = 0;
        int liveTrackCount = 0;

        foreach (Vehicle track in plottingTable.Vehicles)
        {
            if (track == null)
            {
                continue;
            }

            if (track.Identified.Value)
            {
                identifiedCount++;
            }

            if (track.Class.HasValue)
            {
                classFieldPresentCount++;
                if (!string.IsNullOrEmpty(track.Class.Value.Value))
                {
                    classValueCount++;
                }
            }

            if (track.Side.HasValue)
            {
                sideKnownCount++;
            }

            if (track.HasLiveTrack)
            {
                liveTrackCount++;
            }
        }

        Logger.LogInfo(
            "TASK018 summary observer=" + observerSide
            + " missionTimeSeconds=" + GameTime.missionElapsedTime.ToString("F1")
            + " tracks=" + plottingTable.Vehicles.Count
            + " identified=" + identifiedCount
            + " classFieldPresent=" + classFieldPresentCount
            + " classValueNonEmpty=" + classValueCount
            + " sideKnown=" + sideKnownCount
            + " live=" + liveTrackCount);
    }
}
