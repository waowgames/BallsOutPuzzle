public sealed class GameFlowController : SingletonMonoBehaviour<GameFlowController>
{
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
