using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using BepInEx;
using HarmonyLib;
using SeaPower;
using UnityEngine;

namespace NavalCommand.Task012;

[BepInPlugin("dev.navalcommand.task012.engagement-probe", "Task 012 Engagement Probe", "0.1.0")]
public sealed class Task012EngagementProbe : BaseUnityPlugin
{
    private const BindingFlags InstanceFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private const BindingFlags StaticFlags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    private const double UnityToNauticalMiles = 0.03628513216972351;
    private static readonly ConcurrentQueue<string> OutputQueue = new();
    private static Capture activeCapture;
    private static long lastCaptureTicks;
    private static int captureCount;
    private static int discoveryChecks;
    private static bool discoveryComplete;
    private static long lastDiscoveryCheck;
    private static object detectedAmmunition;
    private static object nativeSource;
    private static object nativeTarget;
    private static object nativeTargetTrack;
    private static object nativeWeaponSystem;
    private static int nativeSamples;
    private static double nextNativeSampleSeconds = double.PositiveInfinity;
    private static int ammunitionScanNodes;
    private static bool writerStop;
    private static string runDirectory;
    private static Thread writerThread;

    private Harmony harmony;
    private bool probeEnabled;
    private bool installed;

    private sealed class Capture
    {
        public Capture Previous;
        public object State;
        public object ViewModel;
        public object Track;
        public object Target;
        public object Source;
        public string AmmoName;
        public string AmmoDisplayName;
        public string WeaponSystemTypes;
        public string RandomBefore;
        public string CacheBefore;
        public string WeaponStateBefore;
        public int AmmoBefore;
        public int TasksBefore;
        public long TickBefore;
        public long StartedAt;
        public double MissionSeconds;
        public int ThreadId;
        public double? HitScore;
        public double? KinematicPercent;
        public double? SensorPercent;
        public double? LosPercent;
        public double? TimeOfFlight;
        public double? HorizontalRangeUnity;
        public double? KinematicRangeLatitude;
        public double? ExpectedDeviationFeet;
        public double? NoEscapePercent;
        public string Issues;
    }

    private void Awake()
    {
        if (System.Environment.GetEnvironmentVariable("NC_TASK012_ENABLED") != "1")
        {
            return;
        }

        runDirectory = System.Environment.GetEnvironmentVariable("NC_TASK012_RUN_DIR");
        if (string.IsNullOrWhiteSpace(runDirectory))
        {
            return;
        }

        probeEnabled = true;
        Directory.CreateDirectory(runDirectory);
        writerThread = new Thread(WriteOutput)
        {
            IsBackground = true,
            Name = "Task012 Evidence Writer"
        };
        writerThread.Start();
        Emit("probe-awake thread=" + Thread.CurrentThread.ManagedThreadId);
    }

    private void Update()
    {
        if (!probeEnabled || Globals.mainThread != Thread.CurrentThread)
        {
            return;
        }

        if (!installed)
        {
            installed = true;
            Emit("probe-observer-only main_thread=" + Thread.CurrentThread.ManagedThreadId.ToString(CultureInfo.InvariantCulture));
        }

        long now = Stopwatch.GetTimestamp();
        if (!discoveryComplete && now - lastDiscoveryCheck >= Stopwatch.Frequency / 2)
        {
            lastDiscoveryCheck = now;
            DiscoverNativeInputs();
        }
        else if (discoveryComplete && nativeSource != null && nativeSamples < 2 &&
            GameTime.missionElapsedTime >= nextNativeSampleSeconds)
        {
            RunNativeSample();
        }
    }

    private static void DiscoverNativeInputs()
    {
        try
        {
            object taskforce = StaticMember(typeof(Globals), "_playerTaskforce");
            IEnumerable vehicles = Member(Member(taskforce, "PlottingTable"), "Vehicles") as IEnumerable;
            object source = null;
            object targetTrack = null;
            object target = null;
            int trackCount = 0;
            bool targetClassified = false;
            bool targetIdentified = false;
            if (vehicles != null)
            {
                foreach (object vehicle in vehicles)
                {
                    object baseObject = Member(vehicle, "BaseObject");
                    if (baseObject == null)
                    {
                        continue;
                    }

                    bool playerObject = Bool(Member(baseObject, "IsPlayerObject"));
                    trackCount++;

                    if (playerObject && source == null)
                    {
                        source = baseObject;
                    }
                    else if (!playerObject && target == null)
                    {
                        targetTrack = vehicle;
                        target = baseObject;
                        targetClassified = Bool(Member(vehicle, "IsClassified"));
                        targetIdentified = Bool(Member(Member(vehicle, "Identified"), "Value"));
                    }
                }
            }

            if (source == null || target == null)
            {
                discoveryChecks++;
                if (discoveryChecks == 1 || discoveryChecks % 20 == 0)
                {
                    Emit("native-input-wait tracks=" + trackCount.ToString(CultureInfo.InvariantCulture) +
                        " source=" + (source?.GetType().Name ?? "none") +
                        " target=" + (target?.GetType().Name ?? "none"));
                }
                return;
            }

            Emit("track-summary count=" + trackCount.ToString(CultureInfo.InvariantCulture) +
                " source_category=aircraft target_category=aircraft classified=" + targetClassified.ToString().ToLowerInvariant() +
                " identified=" + targetIdentified.ToString().ToLowerInvariant() +
                " mission_seconds=" + Format(GameTime.missionElapsedTime));
            DumpMethodSignatures("SeaPower.ObjectBase", "GetMaxEngagementRanges", "GetKinematicRange", "GetTerrainRange", "GetSensorRange");
            DumpMethodSignatures("SeaPower.ObjectBase", "GetWeaponSystemsForAmmunition", "getAmmunitionAmountByName");
            DumpMethodSignatures("SeaPower.AmmunitionParameters", "NoEscapeRangePrecise", "CalculateHitChance", "CalculateExpectedDeviation", "MaxRangePrecise");
            DumpMethodSignatures("SeaPower.Ammunition", "GetAmmunitionParameters", "GetParameters", "get_ap", "get_Name");
            DumpMethodSignatures("SeaPower.WeaponSystemHardpoint", "GetAmmunition", "GetAmmunitionParameters", "GetWeapon", "get_Ammunition");
            DumpMethodSignatures("SeapowerUI.ViewModels.AttackingState", "UpdateDisplayedRange", "TryStart");
            DumpMethodSignatures("SeapowerUI.MapUnitViewModel", "UpdateContactMouseoverInfo");
            DumpConstructors("SeaPower.Ammunition");
            DumpTypeShape("SeaPower.Ammunition");
            DumpTypeShape("SeaPower.AmmunitionParameters");
            DumpTypeShape("SeaPower.ObjectBase+EngagementRange");
            DumpTypeShape("SeaPower.MissileSimulator+KinematicRangeResult");
            DumpConstructors("SeapowerUI.ViewModels.AttackingState");
            DumpConstructors("SeapowerUI.ViewModels.WeaponEntry");
            DumpPlatformAmmunition(source);
            if (detectedAmmunition != null)
            {
                nativeSource = source;
                nativeTarget = target;
                nativeTargetTrack = targetTrack;
                RunNativeSample();
                nextNativeSampleSeconds = GameTime.missionElapsedTime + 15.0;
            }
            discoveryComplete = true;
            Emit("native-input-discovery-complete");
        }
        catch (Exception exception)
        {
            Emit("native-input-discovery-error type=" + exception.GetType().Name);
            discoveryComplete = true;
        }
    }

    private static void DumpPlatformAmmunition(object source)
    {
        FieldInfo dictionaryField = source?.GetType().GetField("_ammunitionToWeaponSystemsDictionary", InstanceFlags);
        IDictionary dictionary = dictionaryField?.GetValue(source) as IDictionary;
        if (dictionary == null)
        {
            Emit("ammunition-map unavailable");
            return;
        }

        foreach (DictionaryEntry entry in dictionary)
        {
            string ammunitionName = Convert.ToString(entry.Key, CultureInfo.InvariantCulture) ?? "unknown";
            if (!string.Equals(ammunitionName, "usn_aim-7m", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            object[] systems = (entry.Value as IEnumerable)?.Cast<object>().Where(system => system != null).Take(8).ToArray() ?? Array.Empty<object>();
            int count = 0;
            try
            {
                MethodInfo amountMethod = source.GetType().GetMethods(InstanceFlags).First(method =>
                    method.Name == "getAmmunitionAmountByName" && method.GetParameters().Length == 1);
                count = Convert.ToInt32(amountMethod.Invoke(source, new object[] { ammunitionName }), CultureInfo.InvariantCulture);
            }
            catch
            {
                count = -1;
            }

            Emit("ammunition name=" + ammunitionName + " rounds=" + count.ToString(CultureInfo.InvariantCulture) +
                " systems=" + string.Join(",", systems.Select(system => system.GetType().Name)));
            foreach (object system in systems)
            {
                nativeWeaponSystem = system;
                ammunitionScanNodes = 0;
                detectedAmmunition = FindAmmunitionObject(system, ammunitionName, 4, new HashSet<int>(), 0);
                if (detectedAmmunition != null)
                {
                    nativeWeaponSystem = system;
                    Emit("ammunition-object-found type=" + detectedAmmunition.GetType().Name +
                        " parameters=" + Member(Member(detectedAmmunition, "_ap"), "_ammunitionFileName"));
                    return;
                }
            }
            Emit("ammunition-object-not-found name=" + ammunitionName);
        }

        if (detectedAmmunition == null)
        {
            object parameters = FindSharedAmmunitionParameters("usn_aim-7m");
            Type ammunitionType = FindType("SeaPower.Ammunition");
            ConstructorInfo constructor = ammunitionType?.GetConstructors(InstanceFlags).FirstOrDefault(candidate =>
                candidate.GetParameters().Length == 1 && candidate.GetParameters()[0].ParameterType.FullName == "SeaPower.AmmunitionParameters");
            if (parameters != null && constructor != null)
            {
                detectedAmmunition = constructor.Invoke(new[] { parameters });
                Emit("ammunition-object-created source=shared-parameters-cache name=usn_aim-7m wrapper=SeaPower.Ammunition");
            }
            else
            {
                Emit("shared-ammunition-parameters-unavailable name=usn_aim-7m");
            }
        }
    }

    private static object FindSharedAmmunitionParameters(string ammunitionName)
    {
        Type type = FindType("SeaPower.AmmunitionParameters");
        FieldInfo sharedField = type?.GetField("_sharedByType", StaticFlags);
        IEnumerable entries = sharedField?.GetValue(null) as IEnumerable;
        if (entries == null)
        {
            return null;
        }

        foreach (object entry in entries.Cast<object>().Take(4096))
        {
            object key = Member(entry, "Key");
            string keyName = Convert.ToString(Member(key, "Item1"), CultureInfo.InvariantCulture);
            if (!string.Equals(keyName, ammunitionName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            object parameters = Member(entry, "Value");
            string fileName = Convert.ToString(Member(parameters, "_ammunitionFileName"), CultureInfo.InvariantCulture);
            if (string.Equals(fileName, ammunitionName, StringComparison.OrdinalIgnoreCase))
            {
                return parameters;
            }
        }
        return null;
    }

    private static void RunNativeSample()
    {
        int sampleIndex = ++nativeSamples;
        try
        {
            if (Globals.mainThread != Thread.CurrentThread || !IsPlayerVisibleTrack(nativeTargetTrack))
            {
                Emit("sample-skipped index=" + sampleIndex.ToString(CultureInfo.InvariantCulture) + " reason=thread-or-track");
                return;
            }

            object parameters = Member(detectedAmmunition, "_ap");
            Transform sourceTransform = Member(nativeSource, "transform") as Transform;
            Transform targetTransform = Member(nativeTarget, "transform") as Transform;
            if (parameters == null || sourceTransform == null || targetTransform == null)
            {
                Emit("sample-skipped index=" + sampleIndex.ToString(CultureInfo.InvariantCulture) + " reason=missing-input");
                return;
            }

            Vector3 sourcePosition = sourceTransform.position;
            Vector3 targetPosition = targetTransform.position;
            Vector3 horizontalTarget = new(targetPosition.x, sourcePosition.y, targetPosition.z);
            double horizontalNm = Vector3.Distance(sourcePosition, horizontalTarget) * UnityToNauticalMiles;
            double distance3dNm = Vector3.Distance(sourcePosition, targetPosition) * UnityToNauticalMiles;
            double missionSeconds = GameTime.missionElapsedTime;
            int ammoBefore = AmmunitionCount(nativeSource, "usn_aim-7m");
            int tasksBefore = TaskCount(nativeSource);
            long tickBefore = GameTime.simulationTick;
            string rngBefore = RandomFingerprint();
            string cachesBefore = CacheFingerprint();
            string weaponBefore = NativeWeaponStateFingerprint(nativeWeaponSystem);
            string weaponStatus = Clean(Member(nativeSource, "_weaponStatus"));
            string selectedAmmo = Clean(Member(nativeSource, "_selectedAmmunitionForEngage"));

            MethodInfo rangesMethod = nativeSource.GetType().GetMethods(InstanceFlags)
                .First(method => method.Name == "GetMaxEngagementRanges" && method.GetParameters().Length == 6);
            var stopwatch = Stopwatch.StartNew();
            object ranges = rangesMethod.Invoke(nativeSource, new object[]
            {
                nativeTarget, detectedAmmunition, false, false, true, null
            });
            stopwatch.Stop();
            double rangesMs = stopwatch.Elapsed.TotalMilliseconds;

            string rangeFields = RangeScalarFields(ranges, "", 2, new HashSet<int>());
            double? minRangeUnity = Number(Member(Member(parameters, "_launchRangesInUnity"), "x"));
            double? minRangeNm = minRangeUnity.HasValue ? minRangeUnity.Value * UnityToNauticalMiles : (double?)null;

            MethodInfo hitMethod = parameters.GetType().GetMethods(InstanceFlags)
                .First(method => method.Name == "CalculateHitChance" && method.GetParameters().Length == 2);
            stopwatch.Restart();
            double? hitScore = Number(hitMethod.Invoke(parameters, new[] { nativeSource, nativeTarget }));
            stopwatch.Stop();
            double hitMs = stopwatch.Elapsed.TotalMilliseconds;

            MethodInfo deviationMethod = parameters.GetType().GetMethods(InstanceFlags)
                .First(method => method.Name == "CalculateExpectedDeviation" && method.GetParameters().Length == 3);
            stopwatch.Restart();
            double? deviationFeet = Number(deviationMethod.Invoke(parameters, new object[] { nativeSource, targetPosition, nativeTarget }));
            stopwatch.Stop();
            double deviationMs = stopwatch.Elapsed.TotalMilliseconds;

            double? maxSearchRange = FindRangeCap(ranges);
            double? noEscapeLat = null;
            double noEscapeMs = 0;
            const int noEscapeIterations = 16;
            if (maxSearchRange.HasValue)
            {
                MethodInfo noEscapeMethod = parameters.GetType().GetMethods(InstanceFlags)
                    .First(method => method.Name == "NoEscapeRangePrecise" && method.GetParameters().Length == 4);
                stopwatch.Restart();
                noEscapeLat = Number(noEscapeMethod.Invoke(parameters, new object[]
                {
                    nativeSource, nativeTarget, Convert.ToSingle(maxSearchRange.Value, CultureInfo.InvariantCulture), noEscapeIterations
                }));
                stopwatch.Stop();
                noEscapeMs = stopwatch.Elapsed.TotalMilliseconds;
            }

            int ammoAfter = AmmunitionCount(nativeSource, "usn_aim-7m");
            int tasksAfter = TaskCount(nativeSource);
            long tickAfter = GameTime.simulationTick;
            string rngAfter = RandomFingerprint();
            string cachesAfter = CacheFingerprint();
            string weaponAfter = NativeWeaponStateFingerprint(nativeWeaponSystem);
            bool classified = Bool(Member(nativeTargetTrack, "IsClassified"));
            bool identified = Bool(Member(Member(nativeTargetTrack, "Identified"), "Value"));
            bool uncertaintyPresent = Member(nativeTargetTrack, "ErrorEllipse") != null;

            Emit("sample index=" + sampleIndex.ToString(CultureInfo.InvariantCulture) +
                " mission_seconds=" + Format(missionSeconds) + " operational_time=" + OperationalClock(missionSeconds) +
                " platform=F-14A ammunition=usn_aim-7m rounds=" + ammoBefore.ToString(CultureInfo.InvariantCulture) +
                " target_category=aircraft classified=" + classified.ToString().ToLowerInvariant() +
                " identified=" + identified.ToString().ToLowerInvariant() +
                " error_ellipse_present=" + uncertaintyPresent.ToString().ToLowerInvariant() +
                " horizontal_distance_nm=" + Format(horizontalNm) + " distance_3d_nm=" + Format(distance3dNm) +
                " min_range_nm=" + Format(minRangeNm) + " ranges=" + rangeFields +
                " no_escape_raw_unity=" + Format(noEscapeLat) +
                " no_escape_nm=" + Format(noEscapeLat.HasValue ? noEscapeLat.Value * UnityToNauticalMiles : (double?)null) +
                " no_escape_cap_native=" + Format(maxSearchRange) + " no_escape_iterations=" + noEscapeIterations.ToString(CultureInfo.InvariantCulture) +
                " hit_score=" + Format(hitScore) + " expected_deviation_ft=" + Format(deviationFeet) +
                " weapon_status=" + weaponStatus + " selected_ammunition_state=" + selectedAmmo);
            Emit("sample-cost index=" + sampleIndex.ToString(CultureInfo.InvariantCulture) +
                " main_thread=true range_ms=" + Format(rangesMs) + " hit_ms=" + Format(hitMs) +
                " no_escape_ms=" + Format(noEscapeMs) + " deviation_ms=" + Format(deviationMs) +
                " ammo=" + ammoBefore.ToString(CultureInfo.InvariantCulture) + "->" + ammoAfter.ToString(CultureInfo.InvariantCulture) +
                " tasks=" + tasksBefore.ToString(CultureInfo.InvariantCulture) + "->" + tasksAfter.ToString(CultureInfo.InvariantCulture) +
                " tick_delta=" + (tickAfter - tickBefore).ToString(CultureInfo.InvariantCulture) +
                " rng_changed=" + (rngBefore != rngAfter).ToString().ToLowerInvariant() +
                " caches_changed=" + (cachesBefore != cachesAfter).ToString().ToLowerInvariant() +
                " weapon_state_changed=" + (weaponBefore != weaponAfter).ToString().ToLowerInvariant());
        }
        catch (Exception exception)
        {
            Emit("sample-error index=" + sampleIndex.ToString(CultureInfo.InvariantCulture) + " type=" + exception.GetType().Name);
            nativeSource = null;
        }
    }

    private static string RangeScalarFields(object value, string prefix, int depth, HashSet<int> visited)
    {
        if (value == null || depth < 0 || value is UnityEngine.Object || value is string)
        {
            return "";
        }
        int identity = System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(value);
        if (!visited.Add(identity))
        {
            return "";
        }

        var values = new List<string>();
        foreach (FieldInfo field in value.GetType().GetFields(InstanceFlags).Where(field => !field.IsStatic).Take(48))
        {
            object fieldValue = field.GetValue(value);
            string name = string.IsNullOrEmpty(prefix) ? field.Name : prefix + "." + field.Name;
            double? number = Number(fieldValue);
            if (number.HasValue)
            {
                string formatted = name + "=" + Format(number);
                if (name.IndexOf("Lat", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    formatted += "(" + Format(number.Value * 60.0) + "nm)";
                }
                else if (name.IndexOf("Unity", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    formatted += "(" + Format(number.Value * UnityToNauticalMiles) + "nm)";
                }
                values.Add(formatted);
            }
            else if (fieldValue != null && depth > 0 &&
                (fieldValue.GetType().Name.IndexOf("Range", StringComparison.OrdinalIgnoreCase) >= 0 ||
                 fieldValue.GetType().Name.IndexOf("Sensor", StringComparison.OrdinalIgnoreCase) >= 0 ||
                 fieldValue.GetType().Name.IndexOf("Kinematic", StringComparison.OrdinalIgnoreCase) >= 0))
            {
                string nested = RangeScalarFields(fieldValue, name, depth - 1, visited);
                if (!string.IsNullOrEmpty(nested))
                {
                    values.Add(nested);
                }
            }
            else if (fieldValue is IEnumerable enumerable && field.Name.IndexOf("issue", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                values.Add(name + "=" + Names(enumerable));
            }
        }
        return string.Join(";", values);
    }

    private static double? FindRangeCap(object value)
    {
        string[] candidates = { "MaxRangeInLat", "MaxRange", "KinematicRangeInLat", "KinematicRange", "RangeInLat" };
        foreach (string candidate in candidates)
        {
            object member = Member(value, candidate);
            double? number = Number(member);
            if (number.HasValue && number.Value > 0)
            {
                return number;
            }
        }
        return null;
    }

    private static string NativeWeaponStateFingerprint(object system)
    {
        if (system == null)
        {
            return "unavailable";
        }
        var state = new StringBuilder();
        foreach (FieldInfo field in system.GetType().GetFields(InstanceFlags).Where(field => !field.IsStatic &&
            HasToken(field.Name, "target", "reserve", "ammo", "round", "salvo", "launch", "fire")).OrderBy(field => field.Name))
        {
            state.Append(field.Name).Append('=').Append(Snapshot(field.GetValue(system))).Append(';');
        }
        return Hash(state.ToString());
    }

    private static string OperationalClock(double missionSeconds)
    {
        TimeSpan elapsed = TimeSpan.FromSeconds(Math.Max(0, missionSeconds));
        int hour = (10 + elapsed.Hours) % 24;
        return hour.ToString("00", CultureInfo.InvariantCulture) + ":" +
            elapsed.Minutes.ToString("00", CultureInfo.InvariantCulture) + ":" +
            elapsed.Seconds.ToString("00", CultureInfo.InvariantCulture);
    }

    private static object FindAmmunitionObject(object value, string ammunitionName, int depth, HashSet<int> visited, int nodes)
    {
        ammunitionScanNodes++;
        if (value == null || depth < 0 || ammunitionScanNodes > 256 || value is string || value is UnityEngine.Object)
        {
            return null;
        }

        int identity = System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(value);
        if (!visited.Add(identity))
        {
            return null;
        }

        Type type = value.GetType();
        if (type.FullName == "SeaPower.Ammunition")
        {
            object parameters = Member(value, "_ap");
            string fileName = Convert.ToString(Member(parameters, "_ammunitionFileName"), CultureInfo.InvariantCulture);
            return string.Equals(fileName, ammunitionName, StringComparison.OrdinalIgnoreCase) ? value : null;
        }

        if (value is IDictionary dictionary)
        {
            int count = 0;
            foreach (DictionaryEntry entry in dictionary)
            {
                if (count++ >= 32)
                {
                    break;
                }
                if (string.Equals(Convert.ToString(entry.Key, CultureInfo.InvariantCulture), ammunitionName, StringComparison.OrdinalIgnoreCase))
                {
                    object found = FindAmmunitionObject(entry.Value, ammunitionName, depth - 1, visited, nodes + 1);
                    if (found != null)
                    {
                        return found;
                    }
                }
            }
            return null;
        }

        if (value is IEnumerable enumerable)
        {
            foreach (object item in enumerable.Cast<object>().Take(24))
            {
                object found = FindAmmunitionObject(item, ammunitionName, depth - 1, visited, nodes + 1);
                if (found != null)
                {
                    return found;
                }
            }
            return null;
        }

        if (type.Name.IndexOf("Weapon", StringComparison.OrdinalIgnoreCase) < 0 &&
            type.Name.IndexOf("Ammo", StringComparison.OrdinalIgnoreCase) < 0)
        {
            return null;
        }

        foreach (FieldInfo field in type.GetFields(InstanceFlags).Where(field => !field.IsStatic &&
            HasToken(field.Name, "ammo", "munition", "weapons", "loaded", "associated") &&
            !HasToken(field.Name, "vwp", "weaponSystem")))
        {
            object found = FindAmmunitionObject(field.GetValue(value), ammunitionName, depth - 1, visited, nodes + 1);
            if (found != null)
            {
                return found;
            }
        }
        return null;
    }

    private static void DumpMethodSignatures(string typeName, params string[] names)
    {
        Type type = FindType(typeName);
        if (type == null)
        {
            Emit("type-unavailable name=" + typeName);
            return;
        }

        foreach (MethodInfo method in type.GetMethods(InstanceFlags | StaticFlags)
            .Where(method => names.Contains(method.Name)).OrderBy(method => method.Name))
        {
            string parameters = string.Join(",", method.GetParameters().Select(parameter =>
                parameter.Name + ":" + parameter.ParameterType.FullName));
            Emit("method type=" + typeName + " name=" + method.Name +
                " return=" + method.ReturnType.FullName + " static=" + method.IsStatic.ToString().ToLowerInvariant() +
                " params=" + parameters);
        }
    }

    private static void DumpConstructors(string typeName)
    {
        Type type = FindType(typeName);
        if (type == null)
        {
            return;
        }

        foreach (ConstructorInfo constructor in type.GetConstructors(InstanceFlags))
        {
            string parameters = string.Join(",", constructor.GetParameters().Select(parameter =>
                parameter.Name + ":" + parameter.ParameterType.FullName));
            Emit("constructor type=" + typeName + " params=" + parameters);
        }
    }

    private static void DumpTypeShape(string typeName)
    {
        Type type = FindType(typeName);
        if (type == null)
        {
            Emit("type-shape-unavailable name=" + typeName);
            return;
        }

        string fields = string.Join(",", type.GetFields(InstanceFlags | StaticFlags)
            .Select(field => (field.IsStatic ? "static " : "") + field.Name + ":" + field.FieldType.FullName));
        Emit("type-shape name=" + typeName + " value_type=" + type.IsValueType.ToString().ToLowerInvariant() + " fields=" + fields);
    }

    private static string DescribeFieldValue(object value)
    {
        if (value == null)
        {
            return "null";
        }
        Type type = value.GetType();
        if (type.IsPrimitive || type.IsEnum || value is string)
        {
            return Clean(value);
        }
        if (value is IEnumerable enumerable)
        {
            return type.Name + "[" + enumerable.Cast<object>().Take(12)
                .Count().ToString(CultureInfo.InvariantCulture) + "]";
        }
        return type.Name;
    }

    private static void RangePrefix(object __instance, object[] __args, out Capture __state)
    {
        __state = null;
        try
        {
            if (Globals.mainThread != Thread.CurrentThread || __args == null || __args.Length < 2 ||
                __args[0] == null || __args[0].GetType().FullName != "SeapowerUI.MapUnitViewModel")
            {
                return;
            }

            object viewModel = __args[0];
            object track = Member(viewModel, "Unit");
            object target = Member(track, "BaseObject");
            object source = Member(__instance, "Source");
            object ammo = __args[1];
            if (track == null || target == null || source == null || ammo == null ||
                !Bool(Member(source, "IsPlayerObject")) || !IsPlayerVisibleTrack(track))
            {
                return;
            }

            long now = Stopwatch.GetTimestamp();
            if (captureCount >= 60 || (lastCaptureTicks != 0 && now - lastCaptureTicks < Stopwatch.Frequency))
            {
                return;
            }

            string ammoName = AmmunitionName(ammo);
            var capture = new Capture
            {
                Previous = activeCapture,
                State = __instance,
                ViewModel = viewModel,
                Track = track,
                Target = target,
                Source = source,
                AmmoName = ammoName,
                AmmoDisplayName = Convert.ToString(Member(Member(ammo, "_ap"), "_name"), CultureInfo.InvariantCulture) ?? ammoName,
                WeaponSystemTypes = WeaponSystemTypes(source, ammoName),
                RandomBefore = RandomFingerprint(),
                CacheBefore = CacheFingerprint(),
                WeaponStateBefore = WeaponStateFingerprint(source, ammoName),
                AmmoBefore = AmmunitionCount(source, ammoName),
                TasksBefore = TaskCount(source),
                MissionSeconds = GameTime.missionElapsedTime,
                TickBefore = GameTime.simulationTick,
                ThreadId = Thread.CurrentThread.ManagedThreadId
            };
            capture.StartedAt = Stopwatch.GetTimestamp();
            activeCapture = capture;
            __state = capture;
        }
        catch (Exception exception)
        {
            Emit("range-prefix-error type=" + exception.GetType().Name);
        }
    }

    private static void ContactPrefix(object[] __args)
    {
        Capture capture = activeCapture;
        if (capture == null || __args == null || __args.Length < 10)
        {
            return;
        }

        capture.HitScore = Number(__args[0]);
        capture.KinematicPercent = Number(__args[1]);
        capture.SensorPercent = Number(__args[2]);
        capture.LosPercent = Number(__args[3]);
        capture.TimeOfFlight = Number(__args[4]);
        capture.HorizontalRangeUnity = Number(__args[5]);
        capture.KinematicRangeLatitude = Number(__args[6]);
        capture.ExpectedDeviationFeet = Number(__args[7]);
        capture.Issues = Names(__args[8] as IEnumerable);
        capture.NoEscapePercent = Number(__args[9]);
    }

    private static void RangePostfix(object[] __args, Capture __state)
    {
        if (__state == null)
        {
            return;
        }

        long endedAt = Stopwatch.GetTimestamp();
        try
        {
            (double? distance3dNm, double? horizontalNm) = Distances(__state.Source, __state.Target);
            double? displayedHorizontalNm = __state.HorizontalRangeUnity.HasValue
                ? __state.HorizontalRangeUnity.Value * UnityToNauticalMiles
                : horizontalNm;
            double? kinematicNm = __state.KinematicRangeLatitude * 60.0;
            double? sensorNm = __state.SensorPercent.HasValue && distance3dNm.HasValue
                ? __state.SensorPercent.Value * distance3dNm.Value / 100.0
                : (double?)null;
            double? losNm = __state.LosPercent.HasValue && distance3dNm.HasValue
                ? __state.LosPercent.Value * distance3dNm.Value / 100.0
                : (double?)null;
            double? noEscapeNm = LatitudeToNauticalMiles(Member(__state.State, "NoEscapeRangeInLat"));
            object ellipse = Member(__state.Track, "ErrorEllipse");
            double? uncertaintyNm = Number(Member(ellipse, "SemiMajorAxis")) * 60.0;
            string contactText = Convert.ToString(Member(__state.ViewModel, "ContactMouseoverInfo"), CultureInfo.InvariantCulture) ?? "";
            bool classified = Bool(Member(__state.Track, "IsClassified"));
            bool identified = Bool(Member(Member(__state.Track, "Identified"), "Value"));
            int ammoAfter = AmmunitionCount(__state.Source, __state.AmmoName);
            int tasksAfter = TaskCount(__state.Source);
            long tickAfter = GameTime.simulationTick;
            string randomAfter = RandomFingerprint();
            string cacheAfter = CacheFingerprint();
            string weaponStateAfter = WeaponStateFingerprint(__state.Source, __state.AmmoName);

            var values = new[]
            {
                Format(__state.MissionSeconds), __state.ThreadId.ToString(CultureInfo.InvariantCulture),
                (__state.ThreadId == MainThreadId()).ToString().ToLowerInvariant(),
                __state.Source.GetType().Name, __state.AmmoName, __state.AmmoDisplayName,
                __state.WeaponSystemTypes, "true", classified.ToString().ToLowerInvariant(),
                identified.ToString().ToLowerInvariant(), (Member(__state.Track, "DetectedPosition") != null).ToString().ToLowerInvariant(),
                Format(uncertaintyNm), Format(displayedHorizontalNm), Format(distance3dNm),
                Format(LatitudeToNauticalMiles(Member(__state.State, "MinRangeInLat"))),
                Format(LatitudeToNauticalMiles(Member(__state.State, "MaxRangeInLat"))),
                Format(kinematicNm), Format(sensorNm), Format(losNm), Format(noEscapeNm), Format(__state.TimeOfFlight),
                Format(__state.HitScore), (classified && identified).ToString().ToLowerInvariant(),
                Format(__state.ExpectedDeviationFeet), __state.Issues,
                Clean(Member(__state.State, "MinimumRangeLabel")), Clean(Member(__state.State, "MaximumRangeLabel")),
                Clean(Member(__state.State, "NoEscapeRangeLabel")), Clean(Member(__state.State, "RadarHorizonLabel")),
                Clean(contactText), Format((endedAt - __state.StartedAt) * 1_000_000.0 / Stopwatch.Frequency),
                __state.AmmoBefore.ToString(CultureInfo.InvariantCulture), ammoAfter.ToString(CultureInfo.InvariantCulture),
                __state.TasksBefore.ToString(CultureInfo.InvariantCulture), tasksAfter.ToString(CultureInfo.InvariantCulture),
                (tickAfter - __state.TickBefore).ToString(CultureInfo.InvariantCulture), __state.RandomBefore, randomAfter,
                (__state.RandomBefore != randomAfter).ToString().ToLowerInvariant(), __state.CacheBefore, cacheAfter,
                __state.WeaponStateBefore, weaponStateAfter
            };
            Emit(string.Join("\t", values.Select(Clean)));
            captureCount++;
            lastCaptureTicks = endedAt;
        }
        catch (Exception exception)
        {
            Emit("range-postfix-error type=" + exception.GetType().Name);
        }
        finally
        {
            activeCapture = __state.Previous;
        }
    }

    private static bool IsPlayerVisibleTrack(object track)
    {
        object playerTaskforce = StaticMember(typeof(Globals), "_playerTaskforce");
        IEnumerable vehicles = Member(Member(playerTaskforce, "PlottingTable"), "Vehicles") as IEnumerable;
        return vehicles != null && vehicles.Cast<object>().Any(vehicle => ReferenceEquals(vehicle, track));
    }

    private static (double? distance3dNm, double? horizontalNm) Distances(object source, object target)
    {
        Transform sourceTransform = Member(source, "transform") as Transform;
        Transform targetTransform = Member(target, "transform") as Transform;
        if (sourceTransform == null || targetTransform == null)
        {
            return (null, null);
        }

        Vector3 sourcePosition = sourceTransform.position;
        Vector3 targetPosition = targetTransform.position;
        Vector3 horizontalTarget = new(targetPosition.x, sourcePosition.y, targetPosition.z);
        return (Vector3.Distance(sourcePosition, targetPosition) * UnityToNauticalMiles,
            Vector3.Distance(sourcePosition, horizontalTarget) * UnityToNauticalMiles);
    }

    private static int AmmunitionCount(object source, string name)
    {
        try
        {
            MethodInfo method = source.GetType().GetMethods(InstanceFlags).First(candidate =>
                candidate.Name == "getAmmunitionAmountByName" && candidate.GetParameters().Length == 1);
            return Convert.ToInt32(method.Invoke(source, new object[] { name }), CultureInfo.InvariantCulture);
        }
        catch
        {
            return -1;
        }
    }

    private static int TaskCount(object source)
    {
        IEnumerable tasks = Member(Member(source, "_userRoot"), "TaskViewModels") as IEnumerable;
        return tasks == null ? -1 : tasks.Cast<object>().Count();
    }

    private static string WeaponSystemTypes(object source, string ammoName)
    {
        IEnumerable systems = WeaponSystems(source, ammoName);
        return systems == null ? "unavailable" : string.Join(",", systems.Cast<object>().Select(system => system.GetType().Name));
    }

    private static string WeaponStateFingerprint(object source, string ammoName)
    {
        IEnumerable systems = WeaponSystems(source, ammoName);
        if (systems == null)
        {
            return "unavailable";
        }

        var state = new StringBuilder();
        foreach (object system in systems)
        {
            state.Append(system.GetType().FullName).Append(':');
            foreach (FieldInfo field in system.GetType().GetFields(InstanceFlags).Where(field => !field.IsStatic &&
                HasToken(field.Name, "target", "reserve", "ammo", "round", "salvo", "launch", "fire")).OrderBy(field => field.Name))
            {
                state.Append(field.Name).Append('=').Append(Snapshot(MemberValue(field, system))).Append(';');
            }
        }
        return Hash(state.ToString());
    }

    private static IEnumerable WeaponSystems(object source, string ammoName)
    {
        try
        {
            MethodInfo method = source.GetType().GetMethods(InstanceFlags).First(candidate =>
                candidate.Name == "GetWeaponSystemsForAmmunition" && candidate.GetParameters().Length == 1);
            return method.Invoke(source, new object[] { ammoName }) as IEnumerable;
        }
        catch
        {
            return null;
        }
    }

    private static string RandomFingerprint()
    {
        try
        {
            object systemRandom = StaticMember(typeof(Globals), "_rnd");
            object mathRandom = StaticMember(typeof(Globals), "_unityRandom");
            object unityRandomState = typeof(UnityEngine.Random).GetProperty("state", StaticFlags)?.GetValue(null, null);
            return Hash(Snapshot(systemRandom) + Snapshot(mathRandom) + Snapshot(unityRandomState));
        }
        catch
        {
            return "unavailable";
        }
    }

    private static string CacheFingerprint()
    {
        try
        {
            var state = new StringBuilder();
            string[] types = { "SeaPower.AmmunitionParameters", "SeaPower.ObjectBase", "SeaPower.MissileSimulator", "SeaPower.WeaponSystemMissile", "SeaPower.WeaponSystemGun", "SeaPower.WeaponSystemCIWS" };
            foreach (string typeName in types)
            {
                Type type = FindType(typeName);
                if (type == null)
                {
                    continue;
                }
                foreach (FieldInfo field in type.GetFields(StaticFlags).Where(field => field.IsStatic &&
                    HasToken(field.Name, "cache", "cached", "combined")).OrderBy(field => field.Name))
                {
                    state.Append(typeName).Append('.').Append(field.Name).Append('=')
                        .Append(Snapshot(field.GetValue(null))).Append(';');
                }
            }
            return Hash(state.ToString());
        }
        catch
        {
            return "unavailable";
        }
    }

    private static string Snapshot(object value)
    {
        if (value == null)
        {
            return "null";
        }

        Type type = value.GetType();
        var state = new StringBuilder(type.FullName);
        if (type.IsPrimitive || type.IsEnum || value is string)
        {
            state.Append('=').Append(Convert.ToString(value, CultureInfo.InvariantCulture));
        }
        else if (value is Array array)
        {
            state.Append("[length=").Append(array.Length).Append(':');
            int limit = Math.Min(array.Length, 128);
            for (int index = 0; index < limit; index++)
            {
                state.Append(Convert.ToString(array.GetValue(index), CultureInfo.InvariantCulture)).Append(',');
            }
            state.Append(']');
        }
        else
        {
            foreach (FieldInfo field in type.GetFields(InstanceFlags).Where(field => !field.IsStatic).OrderBy(field => field.Name).Take(32))
            {
                object fieldValue = field.GetValue(value);
                state.Append('|').Append(field.Name).Append('=');
                if (fieldValue == null || fieldValue.GetType().IsPrimitive || fieldValue.GetType().IsEnum || fieldValue is string)
                {
                    state.Append(Convert.ToString(fieldValue, CultureInfo.InvariantCulture));
                }
                else if (fieldValue is Array fieldArray)
                {
                    state.Append('[').Append(fieldArray.Length).Append(':');
                    int limit = Math.Min(fieldArray.Length, 128);
                    for (int index = 0; index < limit; index++)
                    {
                        state.Append(Convert.ToString(fieldArray.GetValue(index), CultureInfo.InvariantCulture)).Append(',');
                    }
                    state.Append(']');
                }
                else
                {
                    state.Append(fieldValue.GetType().FullName).Append('@')
                        .Append(System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(fieldValue));
                }
            }
        }
        return Hash(state.ToString());
    }

    private static object Member(object target, string name)
    {
        if (target == null)
        {
            return null;
        }
        for (Type type = target.GetType(); type != null; type = type.BaseType)
        {
            PropertyInfo property = type.GetProperty(name, InstanceFlags | BindingFlags.DeclaredOnly);
            if (property != null && property.GetIndexParameters().Length == 0)
            {
                return property.GetValue(target, null);
            }
            FieldInfo field = type.GetField(name, InstanceFlags | BindingFlags.DeclaredOnly);
            if (field != null)
            {
                return field.GetValue(target);
            }
        }
        return null;
    }

    private static object MemberValue(FieldInfo field, object target) => field.GetValue(target);

    private static object StaticMember(Type type, string name)
    {
        FieldInfo field = type?.GetField(name, StaticFlags);
        if (field != null)
        {
            return field.GetValue(null);
        }
        return type?.GetProperty(name, StaticFlags)?.GetValue(null, null);
    }

    private static Type FindType(string name)
    {
        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type type = assembly.GetType(name, false);
            if (type != null)
            {
                return type;
            }
        }
        return null;
    }

    private static string AmmunitionName(object ammo)
    {
        return Convert.ToString(Member(Member(ammo, "_ap"), "_ammunitionFileName"), CultureInfo.InvariantCulture) ?? "unknown";
    }

    private static double? LatitudeToNauticalMiles(object value)
    {
        double? latitude = Number(value);
        return latitude.HasValue ? latitude.Value * 60.0 : (double?)null;
    }

    private static double? Number(object value)
    {
        try
        {
            return value == null ? (double?)null : Convert.ToDouble(value, CultureInfo.InvariantCulture);
        }
        catch
        {
            return null;
        }
    }

    private static bool Bool(object value)
    {
        try
        {
            return value != null && Convert.ToBoolean(value, CultureInfo.InvariantCulture);
        }
        catch
        {
            return false;
        }
    }

    private static string Names(IEnumerable values)
    {
        if (values == null)
        {
            return "";
        }
        return string.Join(";", values.Cast<object>().Where(value => value != null).Select(value => value.ToString()));
    }

    private static bool HasToken(string value, params string[] tokens)
    {
        return tokens.Any(token => value.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0);
    }

    private static string Hash(string value)
    {
        using (SHA256 sha = SHA256.Create())
        {
            return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(value))).Replace("-", "").Substring(0, 16);
        }
    }

    private static int MainThreadId()
    {
        return (StaticMember(typeof(Globals), "mainThread") as Thread)?.ManagedThreadId ?? -1;
    }

    private static string Format(double? value)
    {
        return value.HasValue && !double.IsNaN(value.Value) && !double.IsInfinity(value.Value)
            ? value.Value.ToString("F4", CultureInfo.InvariantCulture)
            : "n/a";
    }

    private static string Clean(object value)
    {
        return Convert.ToString(value, CultureInfo.InvariantCulture)?.Replace("\r", " ").Replace("\n", "\\n").Replace("\t", " ") ?? "";
    }

    private static void Emit(string line)
    {
        if (OutputQueue.Count < 256)
        {
            OutputQueue.Enqueue(line);
        }
    }

    private static void WriteOutput()
    {
        try
        {
            using var writer = new StreamWriter(Path.Combine(runDirectory, "task012-hover.tsv"), false, new UTF8Encoding(false));
            while (!writerStop || !OutputQueue.IsEmpty)
            {
                while (OutputQueue.TryDequeue(out string line))
                {
                    writer.WriteLine(line);
                }
                writer.Flush();
                Thread.Sleep(25);
            }
        }
        catch
        {
            // Keep logging failure isolated from the game thread.
        }
    }

    private void OnDestroy()
    {
        harmony?.UnpatchSelf();
        writerStop = true;
        writerThread?.Join(1000);
    }
}
