using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;
using BepInEx;
using SeaPower;
using Unity.Entities;
using UnityEngine;
using EntityWorld = Unity.Entities.World;

namespace NavalCommand.Task020;

[BepInPlugin("dev.navalcommand.task020.observer-recorder", "Task 020 Observer Recorder", "0.1.0")]
public sealed class Task020ObserverRecorder : BaseUnityPlugin
{
    private const float CaptureIntervalSeconds = 0.25f;
    private const float SummaryIntervalSeconds = 10.0f;
    private const float StartupReportIntervalSeconds = 5.0f;
    private const float StartupTimeoutSeconds = 180.0f;
    private const int MaximumContactCount = 4096;
    private const int MaximumQueuedRecords = 8192;
    private const int MaximumStateLength = 8192;

    private readonly Dictionary<int, string> previousStates = new Dictionary<int, string>();
    private readonly HashSet<int> currentTrackIds = new HashSet<int>();
    private readonly List<int> disappearedTrackIds = new List<int>();

    private BlockingCollection<string> recordQueue;
    private Thread writerThread;
    private PropertyInfo viewModelProperty;
    private string observerSide;
    private string scenarioPath;
    private string runDirectory;
    private string recordingPath;
    private float enabledAtRealtimeSeconds;
    private float nextCaptureAtRealtimeSeconds;
    private float nextSummaryAtRealtimeSeconds;
    private float nextStartupReportAtRealtimeSeconds;
    private int droppedRecordCount;
    private int writerFailure;
    private bool enabledForRun;
    private bool captureStarted;
    private bool startupTimeoutWritten;
    private bool maximumContactWarningWritten;
    private bool writerFailureWarningWritten;

    private void Awake()
    {
        enabledForRun = System.Environment.GetEnvironmentVariable("NC_FIXTURE_ENABLED") == "1";
        if (!enabledForRun)
        {
            return;
        }

        observerSide = System.Environment.GetEnvironmentVariable("NC_HARNESS_SIDE") ?? "unknown";
        scenarioPath = System.Environment.GetEnvironmentVariable("NC_HARNESS_SCENARIO") ?? "unknown";
        runDirectory = System.Environment.GetEnvironmentVariable("NC_HARNESS_RUN_DIRECTORY");
        enabledAtRealtimeSeconds = Time.realtimeSinceStartup;

        if (string.IsNullOrWhiteSpace(runDirectory))
        {
            Logger.LogError("Task 020 recorder is enabled but the harness run directory is unavailable.");
            enabledForRun = false;
            return;
        }

        recordingPath = Path.Combine(runDirectory, "task020-observer-state.jsonl");
        recordQueue = new BlockingCollection<string>(MaximumQueuedRecords);
        writerThread = new Thread(WriteRecords)
        {
            IsBackground = true,
            Name = "Task020ObserverRecorderWriter"
        };
        writerThread.Start();

        viewModelProperty = typeof(Vehicle).GetProperty(
            "ViewModel",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        Record("instrumentation-prerequisites", null, DescribePrerequisites());
        Record(
            "session-start",
            null,
            "observer=" + observerSide
            + ";scenario=" + scenarioPath
            + ";gameVersion=" + Application.version
            + ";unityVersion=" + Application.unityVersion
            + ";processId=" + System.Diagnostics.Process.GetCurrentProcess().Id);
        Logger.LogInfo("Task 020 observer-state recording started: " + recordingPath);
    }

    private void Update()
    {
        if (!enabledForRun || Globals.mainThread != Thread.CurrentThread)
        {
            return;
        }

        float realtimeSeconds = Time.realtimeSinceStartup;
        PlottingTable plottingTable = Globals._playerTaskforce?.PlottingTable;
        if (plottingTable == null)
        {
            if (realtimeSeconds >= nextStartupReportAtRealtimeSeconds)
            {
                Record("waiting-for-observer-table", null, "observer=" + observerSide);
                nextStartupReportAtRealtimeSeconds = realtimeSeconds + StartupReportIntervalSeconds;
            }

            if (realtimeSeconds - enabledAtRealtimeSeconds > StartupTimeoutSeconds && !startupTimeoutWritten)
            {
                startupTimeoutWritten = true;
                Record("observer-table-startup-timeout", null, "observer=" + observerSide);
            }

            ReportWriterFailureIfNeeded();
            return;
        }

        if (!captureStarted)
        {
            captureStarted = true;
            nextCaptureAtRealtimeSeconds = realtimeSeconds;
            nextSummaryAtRealtimeSeconds = realtimeSeconds;
            Record(
                "recording-active",
                null,
                "observer=" + observerSide
                + ";missionSeconds=" + GameTime.missionElapsedTime.ToString("F3", CultureInfo.InvariantCulture)
                + ";defaultWorld=" + (EntityWorld.DefaultGameObjectInjectionWorld != null));
        }

        if (realtimeSeconds < nextCaptureAtRealtimeSeconds)
        {
            ReportWriterFailureIfNeeded();
            return;
        }

        nextCaptureAtRealtimeSeconds = realtimeSeconds + CaptureIntervalSeconds;
        bool periodicSnapshot = realtimeSeconds >= nextSummaryAtRealtimeSeconds;
        CaptureObserverContacts(plottingTable, realtimeSeconds, periodicSnapshot);

        if (periodicSnapshot)
        {
            RecordSummary(plottingTable, realtimeSeconds);
            nextSummaryAtRealtimeSeconds = realtimeSeconds + SummaryIntervalSeconds;
        }

        ReportWriterFailureIfNeeded();
    }

    private static string DescribePrerequisites()
    {
        const BindingFlags publicInstance = BindingFlags.Instance | BindingFlags.Public;
        Type vehicleType = typeof(Vehicle);
        FieldInfo entityField = vehicleType.GetField("Entity", publicInstance);
        PropertyInfo entityProperty = vehicleType.GetProperty("Entity", publicInstance);
        FieldInfo currentStateField = typeof(VehicleComponent).GetField("CurrentState", publicInstance);
        PropertyInfo bitsProperty = typeof(ClassInformation).GetProperty("Bits", publicInstance);
        FieldInfo sideField = typeof(DetectedSide).GetField("Side", publicInstance);

        bool getComponentData = HasTypedEntityManagerMethod("GetComponentData");
        bool hasComponent = HasTypedEntityManagerMethod("HasComponent");
        return "Vehicle.Entity.field=" + (entityField != null && entityField.FieldType == typeof(Entity) ? "public-Entity" : "missing")
            + ";Vehicle.Entity.property=" + (entityProperty == null ? "absent" : "present")
            + ";VehicleComponent=" + (typeof(VehicleComponent).IsPublic ? "public-struct" : "unavailable")
            + ";VehicleComponent.CurrentState=" + (currentStateField != null && currentStateField.FieldType == typeof(Entity) ? "public-Entity" : "missing")
            + ";EntityManager.GetComponentData<T>(Entity)=" + (getComponentData ? "available" : "missing")
            + ";EntityManager.HasComponent<T>(Entity)=" + (hasComponent ? "available" : "missing")
            + ";ClassInformation=" + (typeof(ClassInformation).IsPublic ? "public-struct" : "unavailable")
            + ";ClassInformation.Bits=" + (bitsProperty != null && bitsProperty.PropertyType.Name.StartsWith("FixedList512Bytes", StringComparison.Ordinal) ? "available" : "missing")
            + ";DetectedSide=" + (typeof(DetectedSide).IsPublic ? "public-struct" : "unavailable")
            + ";DetectedSide.Side=" + (sideField != null && sideField.FieldType == typeof(Entity) ? "public-Entity" : "missing")
            + ";typedEcsPath=Vehicle.Entity->VehicleComponent.CurrentState";
    }

    private static bool HasTypedEntityManagerMethod(string methodName)
    {
        foreach (MethodInfo method in typeof(EntityManager).GetMethods(BindingFlags.Instance | BindingFlags.Public))
        {
            if (!method.IsGenericMethodDefinition || method.Name != methodName || method.GetGenericArguments().Length != 1)
            {
                continue;
            }

            ParameterInfo[] parameters = method.GetParameters();
            if (parameters.Length == 1 && parameters[0].ParameterType == typeof(Entity))
            {
                return true;
            }
        }

        return false;
    }

    private void CaptureObserverContacts(PlottingTable plottingTable, float realtimeSeconds, bool periodicSnapshot)
    {
        currentTrackIds.Clear();

        foreach (Vehicle track in plottingTable.Vehicles)
        {
            if (track == null)
            {
                continue;
            }

            int trackId = track.Id;
            currentTrackIds.Add(trackId);
            if (!previousStates.ContainsKey(trackId) && previousStates.Count >= MaximumContactCount)
            {
                if (!maximumContactWarningWritten)
                {
                    maximumContactWarningWritten = true;
                    Record("contact-capacity-reached", trackId, "maximum=" + MaximumContactCount);
                }

                continue;
            }

            string currentState = DescribeObserverContact(track);
            if (currentState.Length > MaximumStateLength)
            {
                currentState = currentState.Substring(0, MaximumStateLength) + ";truncated=true";
            }

            bool isNewContact = !previousStates.TryGetValue(trackId, out string previousState);
            bool stateChanged = !string.Equals(previousState, currentState, StringComparison.Ordinal);
            if (isNewContact || stateChanged)
            {
                Record(isNewContact ? "contact-created" : "contact-updated", trackId, currentState, realtimeSeconds);
                previousStates[trackId] = currentState;
            }
            else if (periodicSnapshot)
            {
                Record("contact-snapshot", trackId, currentState, realtimeSeconds);
            }
        }

        disappearedTrackIds.Clear();
        foreach (KeyValuePair<int, string> priorTrack in previousStates)
        {
            if (!currentTrackIds.Contains(priorTrack.Key))
            {
                disappearedTrackIds.Add(priorTrack.Key);
            }
        }

        foreach (int disappearedTrackId in disappearedTrackIds)
        {
            Record("contact-removed", disappearedTrackId, "observer=" + observerSide, realtimeSeconds);
            previousStates.Remove(disappearedTrackId);
        }
    }

    private string DescribeObserverContact(Vehicle track)
    {
        bool classPresent = track.Class.HasValue;
        bool sidePresent = track.Side.HasValue;
        string relationship = "unknown";
        string detectedSide = "unknown";

        StringBuilder state = new StringBuilder(1536);
        state.Append("vehicleEntity=").Append(DescribeEntity(track.Entity));
        state.Append(";identified=").Append(track.Identified.Value);
        state.Append(";classPresent=").Append(classPresent);
        state.Append(";sidePresent=").Append(sidePresent);

        string ecsState = DescribeObserverEcsState(track, out bool hasDetectedSide, out bool sideTaskforceReferencePresent);
        state.Append(";observerEcs=").Append(ecsState);

        if (hasDetectedSide && sideTaskforceReferencePresent && sidePresent && track.Side.Value.Value != null)
        {
            detectedSide = track.Side.Value.Value.Side.ToString();
            if (track.ReportingSide != null)
            {
                relationship = track.ReportingSide.RelationshipTo(track.Side.Value.Value).ToString();
            }
        }

        state.Append(";detectedSide=").Append(detectedSide);
        state.Append(";relationship=").Append(relationship);
        state.Append(";liveTrack=").Append(track.HasLiveTrack);
        state.Append(";dormant=").Append(track.IsDormant.Value);
        state.Append(";positionEstimatePresent=").Append(track.PositionEstimate.HasValue);
        state.Append(";detectingSensors=").Append(track.DetectingSensors.ToString());
        state.Append(";incomingEmissionsInfo=").Append(track.IncomingEmissionsInfo == null ? "null" : track.IncomingEmissionsInfo.Value);
        state.Append(";ui=").Append(DescribeUiPresentation(track));
        return state.ToString();
    }

    private static string DescribeObserverEcsState(
        Vehicle track,
        out bool hasDetectedSide,
        out bool sideTaskforceReferencePresent)
    {
        hasDetectedSide = false;
        sideTaskforceReferencePresent = false;
        try
        {
            EntityWorld gameWorld = EntityWorld.DefaultGameObjectInjectionWorld;
            if (gameWorld == null)
            {
                return "world-unavailable";
            }

            EntityManager entityManager = gameWorld.EntityManager;
            Entity vehicleEntity = track.Entity;
            if (vehicleEntity == Entity.Null || !entityManager.Exists(vehicleEntity))
            {
                return "vehicle-entity-missing;entity=" + DescribeEntity(vehicleEntity);
            }

            if (!entityManager.HasComponent<VehicleComponent>(vehicleEntity))
            {
                return "vehicle-component-missing;entity=" + DescribeEntity(vehicleEntity);
            }

            VehicleComponent vehicleComponent = entityManager.GetComponentData<VehicleComponent>(vehicleEntity);
            Entity currentState = vehicleComponent.CurrentState;
            if (currentState == Entity.Null || !entityManager.Exists(currentState))
            {
                return "current-state-missing;vehicle=" + DescribeEntity(vehicleEntity)
                    + ";currentState=" + DescribeEntity(currentState);
            }

            StringBuilder result = new StringBuilder(768);
            result.Append("vehicle=").Append(DescribeEntity(vehicleEntity));
            result.Append(";currentState=").Append(DescribeEntity(currentState));
            result.Append(";confidence=").Append(vehicleComponent.Confidence.ToString("R", CultureInfo.InvariantCulture));

            bool hasClassInformation = entityManager.HasComponent<ClassInformation>(currentState);
            result.Append(";classInformation=").Append(hasClassInformation);
            if (hasClassInformation)
            {
                ClassInformation information = entityManager.GetComponentData<ClassInformation>(currentState);
                AppendClassInformation(result, information);
            }

            hasDetectedSide = entityManager.HasComponent<DetectedSide>(currentState);
            result.Append(";detectedSideComponent=").Append(hasDetectedSide);
            if (hasDetectedSide)
            {
                DetectedSide side = entityManager.GetComponentData<DetectedSide>(currentState);
                result.Append(";detectedSideEntity=").Append(DescribeEntity(side.Side));
                bool sideEntityExists = side.Side != Entity.Null && entityManager.Exists(side.Side);
                sideTaskforceReferencePresent = sideEntityExists && entityManager.HasComponent<TaskforceReference>(side.Side);
                result.Append(";detectedSideEntityExists=").Append(sideEntityExists);
                result.Append(";detectedSideTaskforceReference=").Append(sideTaskforceReferencePresent);
            }

            result.Append(";detectedPosition=").Append(entityManager.HasComponent<DetectedPosition>(currentState));
            result.Append(";estimatedPosition=").Append(entityManager.HasComponent<EstimatedPosition>(currentState));
            bool hasForcedRelationState = entityManager.HasComponent<ForcedRelationState>(vehicleEntity);
            result.Append(";forcedRelationStatePresent=").Append(hasForcedRelationState);
            if (hasForcedRelationState)
            {
                ForcedRelationState forcedRelation = entityManager.GetComponentData<ForcedRelationState>(vehicleEntity);
                result.Append(";forcedRelationState=").Append(forcedRelation.ForcedState.ToString());
            }

            return result.ToString();
        }
        catch (Exception exception)
        {
            return "read-error=" + exception.GetType().Name;
        }
    }

    private static void AppendClassInformation(StringBuilder result, ClassInformation information)
    {
        result.Append(";overrideIdentification=").Append(information.overrideIdentification);
        result.Append(";preventTaskforceAnalysis=").Append(information._preventTaskforceAnalysis);
        result.Append(";isFalse=").Append(information.isFalse);

        var bits = information.Bits;
        result.Append(";candidateWords=[");
        StringBuilder candidates = new StringBuilder();
        int candidateCount = 0;
        for (int wordIndex = 0; wordIndex < bits.Length; wordIndex++)
        {
            if (wordIndex > 0)
            {
                result.Append(',');
            }

            ulong word = bits[wordIndex];
            result.Append(word.ToString("X16", CultureInfo.InvariantCulture));
            for (int bitIndex = 0; bitIndex < 64; bitIndex++)
            {
                if ((word & (1UL << bitIndex)) == 0)
                {
                    continue;
                }

                if (candidateCount > 0)
                {
                    candidates.Append(',');
                }

                candidates.Append((wordIndex * 64 + bitIndex).ToString(CultureInfo.InvariantCulture));
                candidateCount++;
            }
        }

        result.Append("];candidateCount=").Append(candidateCount);
        result.Append(";candidateIds=[").Append(candidates).Append(']');
    }

    private string DescribeUiPresentation(Vehicle track)
    {
        if (viewModelProperty == null)
        {
            return "view-model-property-unavailable";
        }

        if (!TryGetViewModel(track, out object viewModel, out string readError))
        {
            return readError == null ? "view-model-not-created" : "read-error=" + readError;
        }

        try
        {
            StringBuilder presentation = new StringBuilder(512);
            presentation.Append("type=").Append(viewModel.GetType().FullName);
            AppendUiProperty(presentation, viewModel, "VisualType");
            AppendUiProperty(presentation, viewModel, "VisualPosture");
            AppendUiProperty(presentation, viewModel, "ContactInfoLine1");
            AppendUiProperty(presentation, viewModel, "ContactInfoLine2");
            AppendUiProperty(presentation, viewModel, "ContactInfoLine3");
            AppendUiProperty(presentation, viewModel, "ContactInfoLine4");
            AppendUiProperty(presentation, viewModel, "UnitColorBrush");
            return presentation.ToString();
        }
        catch (Exception exception)
        {
            return "read-error=" + exception.GetType().Name;
        }
    }

    private bool TryGetViewModel(Vehicle track, out object viewModel, out string readError)
    {
        viewModel = null;
        readError = null;
        if (viewModelProperty == null)
        {
            readError = "property-unavailable";
            return false;
        }

        try
        {
            viewModel = viewModelProperty.GetValue(track, null);
            return viewModel != null;
        }
        catch (Exception exception)
        {
            readError = exception.GetType().Name;
            return false;
        }
    }

    private static void AppendUiProperty(StringBuilder builder, object viewModel, string propertyName)
    {
        builder.Append(';').Append(propertyName).Append('=');
        PropertyInfo property = viewModel.GetType().GetProperty(
            propertyName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (property == null)
        {
            builder.Append("missing");
            return;
        }

        object value = property.GetValue(viewModel, null);
        if (value == null)
        {
            builder.Append("null");
            return;
        }

        if (propertyName == "UnitColorBrush")
        {
            PropertyInfo colorProperty = value.GetType().GetProperty("Color", BindingFlags.Instance | BindingFlags.Public);
            object color = colorProperty?.GetValue(value, null);
            builder.Append(color == null ? value.GetType().FullName : color.ToString());
            return;
        }

        Type valueType = value.GetType();
        if (valueType == typeof(string) || valueType.IsPrimitive || valueType.IsEnum)
        {
            builder.Append(Convert.ToString(value, CultureInfo.InvariantCulture));
        }
        else
        {
            builder.Append('<').Append(valueType.FullName).Append('>');
        }
    }

    private static string DescribeEntity(Entity entity)
    {
        return entity == Entity.Null
            ? "null"
            : entity.Index.ToString(CultureInfo.InvariantCulture) + ":" + entity.Version.ToString(CultureInfo.InvariantCulture);
    }

    private void RecordSummary(PlottingTable plottingTable, float realtimeSeconds)
    {
        int identifiedCount = 0;
        int classValueCount = 0;
        int sideKnownCount = 0;
        int liveTrackCount = 0;
        int uiViewModelCount = 0;
        int uiViewModelReadErrorCount = 0;

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
                classValueCount++;
            }

            if (track.Side.HasValue)
            {
                sideKnownCount++;
            }

            if (track.HasLiveTrack)
            {
                liveTrackCount++;
            }

            if (viewModelProperty != null)
            {
                if (TryGetViewModel(track, out object viewModel, out string readError))
                {
                    uiViewModelCount++;
                }
                else if (readError != null)
                {
                    uiViewModelReadErrorCount++;
                }
            }
        }

        Record(
            "periodic-summary",
            null,
            "observer=" + observerSide
            + ";contacts=" + plottingTable.Vehicles.Count
            + ";identified=" + identifiedCount
            + ";classPresent=" + classValueCount
            + ";sideKnown=" + sideKnownCount
            + ";liveTrack=" + liveTrackCount
            + ";uiViewModel=" + uiViewModelCount
            + ";uiViewModelReadErrors=" + uiViewModelReadErrorCount
            + ";droppedRecords=" + Interlocked.CompareExchange(ref droppedRecordCount, 0, 0),
            realtimeSeconds);
    }

    private void Record(string recordType, int? trackId, string details, float? realtimeSeconds = null)
    {
        if (recordQueue == null || recordQueue.IsAddingCompleted)
        {
            return;
        }

        float currentRealtimeSeconds = realtimeSeconds ?? Time.realtimeSinceStartup;
        double missionSeconds = GameTime.missionElapsedTime;
        string trackIdValue = trackId.HasValue ? trackId.Value.ToString(CultureInfo.InvariantCulture) : "null";
        string record = "{\"record\":\"" + EscapeJson(recordType)
            + "\",\"utc\":\"" + DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture)
            + "\",\"realtimeSeconds\":" + currentRealtimeSeconds.ToString("F3", CultureInfo.InvariantCulture)
            + ",\"missionSeconds\":" + missionSeconds.ToString("F3", CultureInfo.InvariantCulture)
            + ",\"observer\":\"" + EscapeJson(observerSide)
            + "\",\"trackId\":" + trackIdValue
            + ",\"details\":\"" + EscapeJson(details)
            + "\"}";

        if (!recordQueue.TryAdd(record))
        {
            Interlocked.Increment(ref droppedRecordCount);
        }
    }

    private void WriteRecords()
    {
        try
        {
            using (FileStream stream = new FileStream(recordingPath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite))
            using (StreamWriter writer = new StreamWriter(stream, new UTF8Encoding(false)))
            {
                foreach (string record in recordQueue.GetConsumingEnumerable())
                {
                    writer.WriteLine(record);
                    writer.Flush();
                }
            }
        }
        catch
        {
            Interlocked.Exchange(ref writerFailure, 1);
        }
    }

    private void ReportWriterFailureIfNeeded()
    {
        if (Interlocked.CompareExchange(ref writerFailure, 0, 0) != 0 && !writerFailureWarningWritten)
        {
            writerFailureWarningWritten = true;
            Logger.LogError("Task 020 recording writer failed; see BepInEx LogOutput.log.");
        }
    }

    private void OnDestroy()
    {
        if (!enabledForRun || recordQueue == null)
        {
            return;
        }

        Record("session-ending", null, "observer=" + observerSide);
        recordQueue.CompleteAdding();
        bool writerStopped = writerThread == null || writerThread.Join(TimeSpan.FromSeconds(5.0));
        if (writerStopped)
        {
            recordQueue.Dispose();
        }
    }

    private static string EscapeJson(string value)
    {
        if (value == null)
        {
            return string.Empty;
        }

        StringBuilder escaped = new StringBuilder(value.Length + 16);
        foreach (char character in value)
        {
            switch (character)
            {
                case '"':
                    escaped.Append("\\\"");
                    break;
                case '\\':
                    escaped.Append("\\\\");
                    break;
                case '\b':
                    escaped.Append("\\b");
                    break;
                case '\f':
                    escaped.Append("\\f");
                    break;
                case '\n':
                    escaped.Append("\\n");
                    break;
                case '\r':
                    escaped.Append("\\r");
                    break;
                case '\t':
                    escaped.Append("\\t");
                    break;
                default:
                    if (character < 0x20)
                    {
                        escaped.Append("\\u").Append(((int)character).ToString("x4", CultureInfo.InvariantCulture));
                    }
                    else
                    {
                        escaped.Append(character);
                    }

                    break;
            }
        }

        return escaped.ToString();
    }
}
