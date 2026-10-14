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

namespace NavalCommand.Task019;

[BepInPlugin("dev.navalcommand.task019.classification-recorder", "Task 019 Classification Recorder", "0.1.0")]
public sealed class Task019ClassificationRecorder : BaseUnityPlugin
{
    private const float CaptureIntervalSeconds = 0.25f;
    private const float SummaryIntervalSeconds = 10.0f;
    private const float StartupReportIntervalSeconds = 5.0f;
    private const float StartupTimeoutSeconds = 180.0f;
    private const int MaximumContactCount = 4096;
    private const int MaximumQueuedRecords = 8192;
    private const int MaximumFormattedFields = 48;
    private const int MaximumArrayItems = 32;
    private const int MaximumNestedFieldDepth = 2;
    private const int MaximumStateLength = 8192;

    private static readonly string[] ObserverStateComponentNames =
    {
        "SeaPower.ClassInformation",
        "SeaPower.DetectedSide",
        "SeaPower.DetectedPosition",
        "SeaPower.EstimatedPosition"
    };

    private readonly Dictionary<int, string> previousStates = new Dictionary<int, string>();
    private readonly HashSet<int> currentTrackIds = new HashSet<int>();
    private readonly List<int> disappearedTrackIds = new List<int>();
    private readonly Dictionary<Type, FieldInfo[]> reflectedFields = new Dictionary<Type, FieldInfo[]>();

    private BlockingCollection<string> recordQueue;
    private Thread writerThread;
    private PropertyInfo vehicleEntityProperty;
    private PropertyInfo vehicleViewModelProperty;
    private MethodInfo getComponentDataMethod;
    private MethodInfo hasComponentMethod;
    private Type vehicleComponentType;
    private Type[] observerStateComponentTypes;
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
            Logger.LogError("Task 019 recorder is enabled but the harness run directory is unavailable.");
            enabledForRun = false;
            return;
        }

        recordingPath = Path.Combine(runDirectory, "task019-native-state.jsonl");
        recordQueue = new BlockingCollection<string>(MaximumQueuedRecords);
        writerThread = new Thread(WriteRecords)
        {
            IsBackground = true,
            Name = "Task019ClassificationRecorderWriter"
        };
        writerThread.Start();

        CacheReflectionMembers();
        Record(
            "session-start",
            null,
            "observer=" + observerSide
            + ";scenario=" + scenarioPath
            + ";gameVersion=" + Application.version
            + ";unityVersion=" + Application.unityVersion
            + ";processId=" + System.Diagnostics.Process.GetCurrentProcess().Id);
        Logger.LogInfo("Task 019 observer-state recording started: " + recordingPath);
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
                + ";missionSeconds=" + GameTime.missionElapsedTime.ToString("F3", CultureInfo.InvariantCulture));
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

    private void CacheReflectionMembers()
    {
        BindingFlags instanceMembers = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        Type vehicleType = typeof(Vehicle);
        vehicleEntityProperty = vehicleType.GetProperty("Entity", instanceMembers);
        vehicleViewModelProperty = vehicleType.GetProperty("ViewModel", instanceMembers);

        Type entityManagerType = typeof(EntityManager);
        foreach (MethodInfo method in entityManagerType.GetMethods(BindingFlags.Instance | BindingFlags.Public))
        {
            if (!method.IsGenericMethodDefinition || method.GetParameters().Length != 1)
            {
                continue;
            }

            ParameterInfo parameter = method.GetParameters()[0];
            if (parameter.ParameterType != typeof(Entity))
            {
                continue;
            }

            if (method.Name == "GetComponentData")
            {
                getComponentDataMethod = method;
            }
            else if (method.Name == "HasComponent")
            {
                hasComponentMethod = method;
            }
        }

        Assembly gameAssembly = typeof(Vehicle).Assembly;
        vehicleComponentType = gameAssembly.GetType("SeaPower.VehicleComponent", false);
        observerStateComponentTypes = new Type[ObserverStateComponentNames.Length];
        for (int index = 0; index < ObserverStateComponentNames.Length; index++)
        {
            observerStateComponentTypes[index] = gameAssembly.GetType(ObserverStateComponentNames[index], false);
        }
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
        string classValue = classPresent ? track.Class.Value.Value ?? string.Empty : string.Empty;
        bool sidePresent = track.Side.HasValue;
        Taskforce detectedTaskforce = sidePresent ? track.Side.Value.Value : null;
        string detectedSide = detectedTaskforce == null ? "unknown" : detectedTaskforce.Side.ToString();
        string relationship = "unknown";

        if (detectedTaskforce != null && track.ReportingSide != null)
        {
            relationship = track.ReportingSide.RelationshipTo(detectedTaskforce).ToString();
        }

        StringBuilder state = new StringBuilder(1024);
        state.Append("identified=").Append(track.Identified.Value);
        state.Append(";classPresent=").Append(classPresent);
        state.Append(";classNonEmpty=").Append(!string.IsNullOrEmpty(classValue));
        state.Append(";class=").Append(classValue);
        state.Append(";sidePresent=").Append(sidePresent);
        state.Append(";detectedSide=").Append(detectedSide);
        state.Append(";relationship=").Append(relationship);
        state.Append(";liveTrack=").Append(track.HasLiveTrack);
        state.Append(";positionEstimatePresent=").Append(track.PositionEstimate.HasValue);
        state.Append(";observerEcs=").Append(DescribeObserverEcsState(track));
        state.Append(";ui=").Append(DescribeUiPresentation(track));
        return state.ToString();
    }

    private string DescribeObserverEcsState(Vehicle track)
    {
        if (vehicleEntityProperty == null || vehicleComponentType == null
            || getComponentDataMethod == null || hasComponentMethod == null)
        {
            return "reflection-unavailable";
        }

        try
        {
            object entityValue = vehicleEntityProperty.GetValue(track, null);
            if (!(entityValue is Entity vehicleEntity))
            {
                return "vehicle-entity-unavailable";
            }

            Unity.Entities.World gameWorld = Unity.Entities.World.DefaultGameObjectInjectionWorld;
            if (gameWorld == null)
            {
                return "world-unavailable";
            }

            EntityManager entityManager = gameWorld.EntityManager;
            object vehicleComponent = GetComponentData(entityManager, vehicleComponentType, vehicleEntity);
            object currentStateValue = GetMemberValue(vehicleComponent, "CurrentState");
            if (!(currentStateValue is Entity currentState))
            {
                return "current-state-unavailable;vehicleComponent=" + FormatSelectedMembers(
                    vehicleComponent,
                    "Confidence",
                    "InitialEpoch");
            }

            StringBuilder componentState = new StringBuilder(512);
            componentState.Append("vehicle=").Append(FormatSelectedMembers(
                vehicleComponent,
                "Confidence",
                "InitialEpoch"));

            for (int index = 0; index < observerStateComponentTypes.Length; index++)
            {
                Type componentType = observerStateComponentTypes[index];
                string componentName = ObserverStateComponentNames[index];
                componentState.Append(';').Append(componentName).Append('=');

                if (componentType == null)
                {
                    componentState.Append("type-unavailable");
                    continue;
                }

                bool componentPresent = HasComponent(entityManager, componentType, currentState);
                componentState.Append(componentPresent);
                if (componentPresent)
                {
                    object component = GetComponentData(entityManager, componentType, currentState);
                    componentState.Append('[').Append(FormatFields(component, 0)).Append(']');
                }
            }

            return componentState.ToString();
        }
        catch (Exception exception)
        {
            Exception cause = exception is TargetInvocationException invocationException
                ? invocationException.InnerException ?? invocationException
                : exception;
            return "read-error=" + cause.GetType().Name;
        }
    }

    private object GetComponentData(EntityManager entityManager, Type componentType, Entity entity)
    {
        MethodInfo closedMethod = getComponentDataMethod.MakeGenericMethod(componentType);
        return closedMethod.Invoke(entityManager, new object[] { entity });
    }

    private bool HasComponent(EntityManager entityManager, Type componentType, Entity entity)
    {
        MethodInfo closedMethod = hasComponentMethod.MakeGenericMethod(componentType);
        return (bool)closedMethod.Invoke(entityManager, new object[] { entity });
    }

    private string DescribeUiPresentation(Vehicle track)
    {
        if (vehicleViewModelProperty == null)
        {
            return "view-model-property-unavailable";
        }

        try
        {
            object viewModel = vehicleViewModelProperty.GetValue(track, null);
            if (viewModel == null)
            {
                return "view-model-not-created";
            }

            StringBuilder presentation = new StringBuilder(384);
            presentation.Append("type=").Append(viewModel.GetType().FullName);
            AppendUiProperty(presentation, viewModel, "VisualType");
            AppendUiProperty(presentation, viewModel, "VisualPosture");
            AppendUiProperty(presentation, viewModel, "Layer");
            AppendUiProperty(presentation, viewModel, "ContactInfoLine1");
            AppendUiProperty(presentation, viewModel, "ContactInfoLine2");
            AppendUiProperty(presentation, viewModel, "ContactInfoLine3");
            AppendUiProperty(presentation, viewModel, "ContactInfoLine4");
            return presentation.ToString();
        }
        catch (Exception exception)
        {
            Exception cause = exception is TargetInvocationException invocationException
                ? invocationException.InnerException ?? invocationException
                : exception;
            return "read-error=" + cause.GetType().Name;
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

    private string FormatSelectedMembers(object value, params string[] memberNames)
    {
        StringBuilder fields = new StringBuilder(128);
        for (int index = 0; index < memberNames.Length; index++)
        {
            if (index > 0)
            {
                fields.Append(',');
            }

            string memberName = memberNames[index];
            fields.Append(memberName).Append('=');
            object memberValue = GetMemberValue(value, memberName);
            fields.Append(FormatValue(memberValue, 0));
        }

        return fields.ToString();
    }

    private string FormatFields(object value, int nestedDepth)
    {
        if (value == null)
        {
            return "null";
        }

        Type valueType = value.GetType();
        FieldInfo[] fields;
        if (!reflectedFields.TryGetValue(valueType, out fields))
        {
            fields = valueType.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            reflectedFields[valueType] = fields;
        }

        StringBuilder description = new StringBuilder(256);
        description.Append(valueType.FullName).Append('{');
        int fieldCount = Math.Min(fields.Length, MaximumFormattedFields);
        for (int index = 0; index < fieldCount; index++)
        {
            if (index > 0)
            {
                description.Append(',');
            }

            FieldInfo field = fields[index];
            description.Append(field.Name).Append('=');
            try
            {
                description.Append(FormatValue(field.GetValue(value), nestedDepth));
            }
            catch
            {
                description.Append("unreadable");
            }
        }

        if (fields.Length > fieldCount)
        {
            description.Append(",moreFields=").Append(fields.Length - fieldCount);
        }

        description.Append('}');
        return description.ToString();
    }

    private string FormatValue(object value, int nestedDepth)
    {
        if (value == null)
        {
            return "null";
        }

        Type valueType = value.GetType();
        if (valueType == typeof(string) || valueType.IsPrimitive || valueType.IsEnum
            || valueType == typeof(decimal))
        {
            return Convert.ToString(value, CultureInfo.InvariantCulture);
        }

        if (value is Array array)
        {
            return FormatArray(array);
        }

        if (valueType.IsValueType && nestedDepth < MaximumNestedFieldDepth)
        {
            return FormatFields(value, nestedDepth + 1);
        }

        return "<" + valueType.FullName + ">";
    }

    private string FormatArray(Array array)
    {
        StringBuilder values = new StringBuilder(128);
        values.Append('[');
        int itemCount = Math.Min(array.Length, MaximumArrayItems);
        for (int index = 0; index < itemCount; index++)
        {
            if (index > 0)
            {
                values.Append(',');
            }

            object item = array.GetValue(index);
            if (item == null)
            {
                values.Append("null");
            }
            else
            {
                Type itemType = item.GetType();
                if (itemType == typeof(string) || itemType.IsPrimitive || itemType.IsEnum)
                {
                    values.Append(Convert.ToString(item, CultureInfo.InvariantCulture));
                }
                else
                {
                    values.Append('<').Append(itemType.FullName).Append('>');
                }
            }
        }

        if (array.Length > itemCount)
        {
            values.Append(",moreItems=").Append(array.Length - itemCount);
        }

        values.Append(']');
        return values.ToString();
    }

    private static object GetMemberValue(object instance, string memberName)
    {
        if (instance == null)
        {
            return null;
        }

        Type instanceType = instance.GetType();
        BindingFlags instanceMembers = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        FieldInfo field = instanceType.GetField(memberName, instanceMembers);
        if (field != null)
        {
            return field.GetValue(instance);
        }

        PropertyInfo property = instanceType.GetProperty(memberName, instanceMembers);
        return property == null ? null : property.GetValue(instance, null);
    }

    private void RecordSummary(PlottingTable plottingTable, float realtimeSeconds)
    {
        int identifiedCount = 0;
        int classValueCount = 0;
        int sideKnownCount = 0;
        int liveTrackCount = 0;
        int uiViewModelCount = 0;

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

            if (track.Class.HasValue && !string.IsNullOrEmpty(track.Class.Value.Value))
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

            if (HasUiViewModel(track))
            {
                uiViewModelCount++;
            }
        }

        Record(
            "periodic-summary",
            null,
            "observer=" + observerSide
            + ";contacts=" + plottingTable.Vehicles.Count
            + ";identified=" + identifiedCount
            + ";classNonEmpty=" + classValueCount
            + ";sideKnown=" + sideKnownCount
            + ";liveTrack=" + liveTrackCount
            + ";uiViewModel=" + uiViewModelCount
            + ";droppedRecords=" + Interlocked.CompareExchange(ref droppedRecordCount, 0, 0),
            realtimeSeconds);
    }

    private bool HasUiViewModel(Vehicle track)
    {
        if (vehicleViewModelProperty == null)
        {
            return false;
        }

        try
        {
            return vehicleViewModelProperty.GetValue(track, null) != null;
        }
        catch
        {
            return false;
        }
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
            using (FileStream stream = new FileStream(
                recordingPath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.ReadWrite))
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
            Logger.LogError("Task 019 recording writer failed; see BepInEx LogOutput.log.");
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
