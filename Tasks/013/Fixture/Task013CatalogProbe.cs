using System;
using System.Linq;
using System.Threading;
using BepInEx;
using SeaPower;
using UnityEngine;

namespace NavalCommand.Task013;

[BepInPlugin("dev.navalcommand.task013.catalog-probe", "Task 013 Catalog Probe", "0.1.0")]
public sealed class Task013CatalogProbe : BaseUnityPlugin
{
    private const float CaptureIntervalSeconds = 0.25f;
    private const float CaptureTimeoutSeconds = 45f;
    private const string AircraftIniName = "usn_f-14a";

    private float enabledAtSeconds;
    private float nextCaptureAtSeconds;
    private bool enabledForRun;
    private bool catalogCaptured;
    private bool ownUnitCaptured;
    private bool trackCaptured;
    private bool timeoutReported;

    private void Awake()
    {
        enabledForRun = System.Environment.GetEnvironmentVariable("NC_FIXTURE_ENABLED") == "1";
        enabledAtSeconds = Time.realtimeSinceStartup;

        if (enabledForRun)
        {
            Logger.LogInfo("TASK013 fixture enabled; captures are read-only and game-thread scoped.");
        }
    }

    private void Update()
    {
        if (!enabledForRun || Globals.mainThread != Thread.CurrentThread)
        {
            return;
        }

        float now = Time.realtimeSinceStartup;
        if (now < nextCaptureAtSeconds)
        {
            return;
        }

        nextCaptureAtSeconds = now + CaptureIntervalSeconds;
        CaptureCatalogWhenReady();
        CaptureOwnUnitWhenReady();
        CaptureEnemyTrackWhenVisible();

        if (!timeoutReported && now - enabledAtSeconds >= CaptureTimeoutSeconds)
        {
            timeoutReported = true;
            Logger.LogInfo(
                "TASK013 timeout catalogCaptured=" + catalogCaptured
                + " ownUnitCaptured=" + ownUnitCaptured
                + " trackCaptured=" + trackCaptured);
        }
    }

    private void CaptureCatalogWhenReady()
    {
        if (catalogCaptured || !Singleton<UnitMetadataProvider>.InstanceExists())
        {
            return;
        }

        UnitMetadataProvider provider = Singleton<UnitMetadataProvider>.Instance;
        if (provider.UnitData == null || UnitMetadataProvider.FindCached("aircraft", AircraftIniName) == null)
        {
            return;
        }

        if (!Singleton<SensorMetadataProvider>.InstanceExists())
        {
            return;
        }

        SensorMetadataProvider sensors = Singleton<SensorMetadataProvider>.Instance;
        if (sensors.Sensors == null || sensors.Sensors.Count == 0)
        {
            return;
        }

        int baseUnitCount = provider.UnitData.Keys.Count(key => !key.Item2.Contains("@"));
        int loadoutExpandedEntryCount = provider.UnitData.Count - baseUnitCount;
        string typeCounts = string.Join(",", provider.UnitData.Values
            .Where(value => value.Item2 != null)
            .GroupBy(value => value.Item2._objectType)
            .OrderBy(group => group.Key.ToString())
            .Select(group => group.Key + ":" + group.Count()));

        Logger.LogInfo(
            "TASK013 catalog unitEntries=" + provider.UnitData.Count
            + " baseKeys=" + baseUnitCount
            + " syntheticLoadoutKeys=" + loadoutExpandedEntryCount
            + " ammunitionDefinitions=" + provider.AmmunitionData.Count
            + " sensorDefinitions=" + sensors.Sensors.Count
            + " objectTypes=" + typeCounts);

        LogTypeSample(provider, "aircraft", AircraftIniName, "F-14A");
        LogTypeSample(provider, "vessels", "usn_ffg_oliver_hazard_perry", "Perry");
        LogTypeSample(provider, "vessels", "usn_ssn_los_angeles", "LosAngeles");

        catalogCaptured = true;
    }

    private void LogTypeSample(UnitMetadataProvider provider, string folder, string iniName, string label)
    {
        SimpleObjectParameters parameters = null;
        int metadataId = -1;

        foreach (var entry in provider.UnitData)
        {
            if (entry.Key.Item1 == folder && entry.Key.Item2 == iniName)
            {
                metadataId = entry.Value.Item1;
                parameters = entry.Value.Item2;
                break;
            }
        }

        if (parameters == null)
        {
            Logger.LogInfo("TASK013 typeSample " + label + " missingFromCatalog=true");
            return;
        }

        string loadouts = string.Join(";", parameters._loadouts.Values
            .OrderBy(loadout => loadout.Name, StringComparer.OrdinalIgnoreCase)
            .Select(loadout => loadout.Name + "[" + DescribeKnownAmmunition(loadout.AmmunitionAmounts) + "]"));

        Logger.LogInfo(
            "TASK013 typeSample " + label
            + " key=" + folder + "/" + iniName
            + " metadataId=" + metadataId
            + " platform=" + parameters._objectType
            + " loadouts=" + loadouts);
    }

    private string DescribeKnownAmmunition(System.Collections.Generic.Dictionary<string, int> quantities)
    {
        string[] relevantNames =
        {
            "usn_aim-7m",
            "usn_aim-9l",
            "usn_aim-54a",
            "all_air_chaff_standard",
            "usn_cal_20mm",
            "usn_mk48_mod4_adcap",
            "usn_ugm-84a",
            "usn_ugm-84c",
            "usn_ugm-109b",
            "usn_ugm-109c",
            "usn_moss",
            "usn_adc_mk1_noisemaker"
        };

        return string.Join(",", quantities
            .Where(entry => relevantNames.Contains(entry.Key, StringComparer.OrdinalIgnoreCase))
            .OrderBy(entry => entry.Key, StringComparer.OrdinalIgnoreCase)
            .Select(entry => entry.Key + "=" + entry.Value));
    }

    private void CaptureOwnUnitWhenReady()
    {
        if (ownUnitCaptured || Globals._playerTaskforce == null)
        {
            return;
        }

        ObjectBase ownAircraft = Globals._playerTaskforce.TaskforceObjects.FirstOrDefault(unit =>
            unit != null
            && unit._obp != null
            && unit._obp.Simple != null
            && unit._obp.Simple._objectIniName == AircraftIniName);

        if (ownAircraft == null)
        {
            return;
        }

        string ammunition = string.Join(",", ownAircraft.AmmunitionAmountDictionary
            .OrderBy(entry => entry.Key, StringComparer.OrdinalIgnoreCase)
            .Select(entry => entry.Key + "=" + entry.Value));

        string sensors = string.Join(",", ownAircraft._obp._sensorSystems
            .OrderBy(system => system._systemName, StringComparer.OrdinalIgnoreCase)
            .Select(system => system._systemName
                + ":" + system.GetType().Name
                + ":on=" + system.IsOn.Value
                + ":inoperable=" + system.Inoperable.Value));

        Logger.LogInfo(
            "TASK013 ownUnit platform=" + ownAircraft._type
            + " loadout=" + ownAircraft._obp._loadoutVariant
            + " ammunition=" + ammunition
            + " sensorSystems=" + sensors
            + " weaponSystemCount=" + ownAircraft._obp._weaponSystems.Count);

        ownUnitCaptured = true;
    }

    private void CaptureEnemyTrackWhenVisible()
    {
        if (trackCaptured || Globals._playerTaskforce?.PlottingTable == null)
        {
            return;
        }

        Vehicle enemyTrack = Globals._playerTaskforce.PlottingTable.Vehicles.FirstOrDefault(track =>
            track != null
            && track.UnitTaskforce.Value != null
            && track.UnitTaskforce.Value != Globals._playerTaskforce);

        if (enemyTrack == null)
        {
            return;
        }

        bool classVisible = enemyTrack.Identified.Value && enemyTrack.Class.HasValue;
        string visibleClass = classVisible ? enemyTrack.Class.Value.Value : "unknown";

        Logger.LogInfo(
            "TASK013 playerTrack identified=" + enemyTrack.Identified.Value
            + " classVisible=" + classVisible
            + " visibleClass=" + visibleClass
            + " liveTrack=" + enemyTrack.HasLiveTrack
            + " positionEstimatePresent=" + enemyTrack.PositionEstimate.HasValue
            + " fieldsRead=Identification,Class,LiveTrack,PositionEstimate"
            + " privilegedObjectEquipmentRead=false");

        trackCaptured = true;
    }
}
