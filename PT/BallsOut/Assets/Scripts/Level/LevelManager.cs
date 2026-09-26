using UnityEngine;

/// <summary>
/// Owns the generic level lifecycle and saved progression.
/// Game-specific systems decide when to complete or fail a level.
/// </summary>
public sealed class LevelManager : SingletonMonoBehaviour<LevelManager>
{
    [SerializeField] private LevelConfig config;

    public int CurrentLevelIndex { get; private set; }
    public int DisplayedLevelNumber => CurrentLevelIndex + 1;
    public int DisplayedLevel1Based => Mathf.Max(1, DisplayedLevelNumber);
    public int CurrentAttempt { get; private set; }
    public LevelData CurrentLevelData { get; private set; }
    public LevelDifficulty CurrentDifficulty => CurrentLevelData != null
        ? CurrentLevelData.Difficulty
        : LevelDifficulty.Normal;
    public LevelState State { get; private set; } = LevelState.Uninitialized;

    protected override void Awake()
    {
        base.Awake();
        CurrentLevelIndex = SaveService.Instance != null
            ? SaveService.Instance.CurrentLevelIndex
            : 0;
    }

    private void Start()
    {
        EnsureCurrentLevelLoaded();
    }

    public void EnsureCurrentLevelLoaded()
    {
        if (State == LevelState.Uninitialized)
            LoadLevel(CurrentLevelIndex);
    }

    public void LoadLevel(int index)
    {
        LoadLevel(index, resetAttempt: true);
    }

    private void LoadLevel(int index, bool resetAttempt)
    {
        CurrentLevelIndex = Mathf.Max(0, index);
        if (resetAttempt)
            CurrentAttempt = 0;

        CurrentLevelData = config != null ? config.GetLevel(CurrentLevelIndex) : null;
        State = LevelState.Loaded;

        SaveProgress();
        GameEvents.RaiseLevelLoaded(CurrentLevelIndex);
    }

    public void StartLevel()
    {
        EnsureCurrentLevelLoaded();

        if (State != LevelState.Loaded)
            return;

        CurrentAttempt++;
        State = LevelState.Playing;
        GameEvents.RaiseLevelStarted(CurrentLevelIndex);
    }

    public void CompleteLevel()
    {
        if (State != LevelState.Playing)
            return;

        int completedLevelIndex = CurrentLevelIndex;
        State = LevelState.Completed;
        CurrentLevelIndex++;

        SaveProgress();
        GameEvents.RaiseLevelCompleted(completedLevelIndex);
    }

    public void FailLevel()
    {
        if (State != LevelState.Playing)
            return;

        int failedLevelIndex = CurrentLevelIndex;
        State = LevelState.Failed;

        GameEvents.RaiseLevelFailed(failedLevelIndex);
        LevelFailPopup.ShowIfAvailable();
    }

    public void RetryLevel()
    {
        if (State != LevelState.Failed)
            return;

        int retryLevelIndex = CurrentLevelIndex;
        GameEvents.RaiseLevelRetried(retryLevelIndex);
        LoadLevel(retryLevelIndex, resetAttempt: false);
        StartLevel();
    }

    public int CurrentLoopCount => config != null
        ? config.GetLoopCount(CurrentLevelIndex)
        : 0;

    private void SaveProgress()
    {
        SaveService.Instance?.SetCurrentLevelIndex(CurrentLevelIndex);
    }
}
