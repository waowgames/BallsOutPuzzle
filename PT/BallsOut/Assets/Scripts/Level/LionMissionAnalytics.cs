using System.Collections.Generic;
using System.Globalization;
using LionStudios.Suite.Analytics;
using UnityEngine;

internal static class LionMissionAnalytics
{
    private const string MissionType = "main";
    private const string OpenMissionKey = "ballsout.analytics.open_mission";
    private const string ProgressKey = "ballsout.analytics.mission_progress";
    private static int missionId;
    private static int attempt;
    private static string missionName;
    private static bool isOpen;
    private static float progress;

    private static string AttemptKey(int id) => "ballsout.analytics.attempt." + id.ToString(CultureInfo.InvariantCulture);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RestoreInterruptedAttempt()
    {
        // Mobile OS termination may skip both OnApplicationQuit and OnDestroy.
        missionId = PlayerPrefs.GetInt(OpenMissionKey);
        isOpen = missionId > 0;
        if (isOpen)
        {
            SetMission(missionId);
            progress = Mathf.Clamp01(PlayerPrefs.GetFloat(ProgressKey));
            Abandon();
        }
        missionId = 0;
    }

    private static void SetMission(int id)
    {
        missionId = id;
        missionName = "main_" + id.ToString(CultureInfo.InvariantCulture);
        attempt = PlayerPrefs.GetInt(AttemptKey(id));
    }

    public static void Begin(int levelNumber)
    {
        SetMission(levelNumber);
        attempt++;
        isOpen = true;
        progress = 0f;
        PlayerPrefs.SetInt(AttemptKey(missionId), attempt);
        PlayerPrefs.SetInt(OpenMissionKey, missionId);
        PlayerPrefs.SetFloat(ProgressKey, progress);
        PlayerPrefs.Save();
        LionAnalytics.MissionStarted(MissionType, missionName, missionId, attempt, isGamePlay: true);
        LionEconomyAnalytics.ReportOpeningInventory();
    }

    public static void BoxRemoved(int totalBoxes, int remainingBoxes)
    {
        if (!isOpen || totalBoxes <= 0) return;
        progress = Mathf.Clamp01((totalBoxes - remainingBoxes) / (float)totalBoxes);
        // SaveService flushes PlayerPrefs on suspend. Resuming continues the same attempt.
        PlayerPrefs.SetFloat(ProgressKey, progress);
    }

    public static void TutorialStep()
    {
        if (isOpen)
            LionAnalytics.MissionStep(MissionType, missionName, missionId, attempt, GameplayData(),
                stepName: "ftue", isGamePlay: true);
    }

    public static void Complete()
    {
        if (!isOpen) return;
        progress = 1f;
        LionAnalytics.MissionCompleted(MissionType, missionName, missionId, attempt, isGamePlay: true);
        Close();
    }

    public static void Fail(string reason)
    {
        if (!isOpen) return;
        LionAnalytics.MissionFailed(MissionType, missionName, missionId, attempt, GameplayData(),
            failReason: reason, isGamePlay: true);
        Close();
    }

    public static void Abandon()
    {
        if (!isOpen) return;
        LionAnalytics.MissionAbandoned(MissionType, missionName, missionId, attempt, GameplayData(), isGamePlay: true);
        Close();
    }

    private static void Close()
    {
        isOpen = false;
        PlayerPrefs.DeleteKey(OpenMissionKey);
        PlayerPrefs.Save();
    }

    public static void PowerUpUsed(string name)
    {
        if (isOpen)
            LionAnalytics.PowerUpUsed(missionId.ToString(CultureInfo.InvariantCulture), MissionType, attempt,
                name, missionName, GameplayData());
    }

    private static Dictionary<string, object> GameplayData() => new Dictionary<string, object>
    {
        ["gameplay_data"] = new Dictionary<string, object> { ["mission_progress"] = progress }
    };

    // The win popup credits coins after the level manager advances to the next level.
    public static Dictionary<string, object> EconomyData()
    {
        if (missionId == 0) return null;
        var data = GameplayData();
        data["mission_data"] = new Dictionary<string, object>
        {
            ["mission_type"] = MissionType, ["mission_name"] = missionName,
            ["mission_id"] = missionId, ["mission_attempt"] = attempt
        };
        return data;
    }
}
