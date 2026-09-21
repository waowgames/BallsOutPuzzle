using UnityEngine;

[DefaultExecutionOrder(-1000)]
public sealed class SaveService : SingletonMonoBehaviour<SaveService>
{
    private const int CurrentVersion = 1;
    private const string PrimaryKey = "template_save_v1";
    private const string BackupKey = "template_save_v1_backup";

    private GameSaveData data;

    public int CurrentLevelIndex => data != null ? data.currentLevelIndex : 0;
    public int SoftCurrency => data != null ? data.softCurrency : 0;
    public bool MusicEnabled => data == null || data.musicEnabled;
    public bool SfxEnabled => data == null || data.sfxEnabled;
    public bool VibrationEnabled => data == null || data.vibrationEnabled;

    protected override void Awake()
    {
        base.Awake();
        if (Instance != this)
            return;

        data = LoadData();
    }

    public void SetCurrentLevelIndex(int value)
    {
        int safeValue = Mathf.Max(0, value);
        if (data == null || data.currentLevelIndex == safeValue)
            return;

        data.currentLevelIndex = safeValue;
        WriteData(true);
    }

    public void SetSoftCurrency(int value)
    {
        int safeValue = Mathf.Max(0, value);
        if (data == null || data.softCurrency == safeValue)
            return;

        data.softCurrency = safeValue;
        WriteData(true);
    }

    public void SetMusicEnabled(bool value)
    {
        if (data == null || data.musicEnabled == value)
            return;

        data.musicEnabled = value;
        WriteData(true);
    }

    public void SetSfxEnabled(bool value)
    {
        if (data == null || data.sfxEnabled == value)
            return;

        data.sfxEnabled = value;
        WriteData(true);
    }

    public void SetVibrationEnabled(bool value)
    {
        if (data == null || data.vibrationEnabled == value)
            return;

        data.vibrationEnabled = value;
        WriteData(true);
    }

    public int GetBoosterCount(string id)
    {
        if (data == null || string.IsNullOrWhiteSpace(id))
            return 0;

        if (data.TryGetBoosterCount(id, out int savedCount))
            return savedCount;

        string legacyKey = $"booster_{id}_count";
        if (!PlayerPrefs.HasKey(legacyKey))
            return 0;

        int migratedCount = Mathf.Max(0, PlayerPrefs.GetInt(legacyKey, 0));
        data.SetBoosterCount(id, migratedCount);
        WriteData(true);
        return migratedCount;
    }

    public void SetBoosterCount(string id, int value)
    {
        if (data == null || string.IsNullOrWhiteSpace(id))
            return;

        int safeValue = Mathf.Max(0, value);
        if (data.TryGetBoosterCount(id, out int currentValue) &&
            currentValue == safeValue)
            return;

        data.SetBoosterCount(id, safeValue);
        WriteData(true);
    }

    public void Flush()
    {
        PlayerPrefs.Save();
    }

    private GameSaveData LoadData()
    {
        if (TryLoad(PrimaryKey, out GameSaveData loaded))
        {
            loaded.Sanitize();
            return loaded;
        }

        if (TryLoad(BackupKey, out loaded))
        {
            loaded.Sanitize();
            data = loaded;
            PlayerPrefs.DeleteKey(PrimaryKey);
            WriteData(true);
            return loaded;
        }

        GameSaveData migrated = new GameSaveData
        {
            version = CurrentVersion,
            currentLevelIndex = Mathf.Max(0, PlayerPrefs.GetInt("lm_currentLevel", 0)),
            softCurrency = Mathf.Max(0, PlayerPrefs.GetInt("score", 0)),
            musicEnabled = PlayerPrefs.GetInt("BgMusicOn", 1) == 1,
            sfxEnabled = PlayerPrefs.GetInt("SfxOn", 1) == 1,
            vibrationEnabled = PlayerPrefs.GetInt("VibrationOn", 1) == 1
        };

        data = migrated;
        WriteData(true);
        return migrated;
    }

    private static bool TryLoad(string key, out GameSaveData loaded)
    {
        loaded = null;
        if (!PlayerPrefs.HasKey(key))
            return false;

        string json = PlayerPrefs.GetString(key, string.Empty);
        if (string.IsNullOrWhiteSpace(json))
            return false;

        try
        {
            loaded = JsonUtility.FromJson<GameSaveData>(json);
            return loaded != null && loaded.version > 0;
        }
        catch (System.Exception exception)
        {
            DebugLogger.LogWarning(
                $"[SaveService] Could not read '{key}': {exception.Message}");
            loaded = null;
            return false;
        }
    }

    private void WriteData(bool flush)
    {
        if (data == null)
            return;

        data.version = CurrentVersion;
        data.Sanitize();

        if (PlayerPrefs.HasKey(PrimaryKey))
            PlayerPrefs.SetString(BackupKey, PlayerPrefs.GetString(PrimaryKey));

        PlayerPrefs.SetString(PrimaryKey, JsonUtility.ToJson(data));
        if (flush)
            PlayerPrefs.Save();
    }

    private void OnApplicationPause(bool paused)
    {
        if (paused)
            Flush();
    }

    private void OnApplicationQuit()
    {
        Flush();
    }
}
