using UnityEngine;

public sealed class GameFlowController : SingletonMonoBehaviour<GameFlowController>
{
    [SerializeField] private bool startOnSceneLoad;

    private void Start()
    {
        if (startOnSceneLoad) StartCurrentLevel();
    }

    public bool StartCurrentLevel()
    {
        LevelManager manager = LevelManager.Instance;
        if (manager == null)
            return false;

        manager.EnsureCurrentLevelLoaded();
        manager.StartLevel();
        return manager.State == LevelState.Playing;
    }

    public bool RetryCurrentLevel()
    {
        LevelManager manager = LevelManager.Instance;
        if (manager == null || manager.State != LevelState.Failed)
            return false;

        manager.RetryLevel();
        return manager.State == LevelState.Playing;
    }

    public bool ContinueToNextLevel()
    {
        LevelManager manager = LevelManager.Instance;
        if (manager == null || manager.State != LevelState.Completed)
            return false;

        manager.LoadLevel(manager.CurrentLevelIndex);
        manager.StartLevel();
        return manager.State == LevelState.Playing;
    }
}
