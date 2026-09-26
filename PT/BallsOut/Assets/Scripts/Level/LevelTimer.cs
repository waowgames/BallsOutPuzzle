using UnityEngine;

/// <summary>
/// Counts down the active level's time limit and fails the level when it runs out.
/// </summary>
public sealed class LevelTimer : SingletonMonoBehaviour<LevelTimer>
{
    public float RemainingSeconds { get; private set; }
    public bool HasTimeLimit { get; private set; }
    public bool IsFrozen => freezeRemaining > 0f;
    public float FreezeRemaining => freezeRemaining;
    /// <summary>Length of the current freeze, used to normalise <see cref="FreezeRemaining"/>.</summary>
    public float FreezeDuration { get; private set; }

    private bool running;
    private float freezeRemaining;

    private void OnEnable()
    {
        GameEvents.OnLevelLoaded += HandleLevelLoaded;
        GameEvents.OnLevelStarted += HandleLevelStarted;
        GameEvents.OnLevelCompleted += HandleLevelEnded;
        GameEvents.OnLevelFailed += HandleLevelEnded;
    }

    private void OnDisable()
    {
        GameEvents.OnLevelLoaded -= HandleLevelLoaded;
        GameEvents.OnLevelStarted -= HandleLevelStarted;
        GameEvents.OnLevelCompleted -= HandleLevelEnded;
        GameEvents.OnLevelFailed -= HandleLevelEnded;
    }

    private void Update()
    {
        if (!running)
            return;

        LevelManager manager = LevelManager.Instance;
        if (manager == null || manager.State != LevelState.Playing)
        {
            running = false;
            return;
        }

        // Popups (e.g. booster shop) and the hard-level intro pause the clock.
        if (UIManager.Instance != null && UIManager.Instance.IsGameplayBlocked)
            return;

        if (freezeRemaining > 0f)
        {
            freezeRemaining -= Time.deltaTime;
            return;
        }

        RemainingSeconds = Mathf.Max(0f, RemainingSeconds - Time.deltaTime);
        if (RemainingSeconds > 0f)
            return;

        running = false;
        manager.FailLevel();
    }

    /// <summary>Pauses the countdown for the given duration (time-freeze booster).</summary>
    public void Freeze(float seconds)
    {
        if (seconds <= freezeRemaining)
            return;

        freezeRemaining = seconds;
        FreezeDuration = seconds;
    }

    public void AddTime(float seconds)
    {
        if (HasTimeLimit)
            RemainingSeconds = Mathf.Max(0f, RemainingSeconds + seconds);
    }

    private void HandleLevelLoaded(int levelIndex)
    {
        running = false;
        ResetToLimit();
    }

    private void HandleLevelStarted(int levelIndex)
    {
        ResetToLimit();
        running = HasTimeLimit;
    }

    private void HandleLevelEnded(int levelIndex)
    {
        running = false;
        freezeRemaining = 0f;
    }

    private void ResetToLimit()
    {
        LevelData data = LevelManager.Instance != null ? LevelManager.Instance.CurrentLevelData : null;
        float limit = data != null ? data.TimeLimitSeconds : 0f;

        HasTimeLimit = limit > 0f;
        RemainingSeconds = limit;
        freezeRemaining = 0f;
    }
}
