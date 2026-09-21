using UnityEngine;

public sealed class LevelContentLoader : MonoBehaviour
{
    [SerializeField] private Transform levelRoot;

    public LevelData CurrentLevelData { get; private set; }
    public GameObject CurrentInstance { get; private set; }

    private void OnEnable()
    {
        GameEvents.OnLevelLoaded += HandleLevelLoaded;
    }

    private void OnDisable()
    {
        GameEvents.OnLevelLoaded -= HandleLevelLoaded;
        ClearCurrent();
    }

    public bool Load(LevelData levelData)
    {
        ClearCurrent();
        CurrentLevelData = levelData;

        if (levelData == null || levelData.LevelPrefab == null)
        {
            DebugLogger.LogWarning(
                "[LevelContentLoader] No level prefab assigned; using scene-authored gameplay.");
            return false;
        }

        Transform parent = levelRoot != null ? levelRoot : transform;
        CurrentInstance = Instantiate(levelData.LevelPrefab, parent);
        return true;
    }

    public void ClearCurrent()
    {
        if (CurrentInstance != null)
        {
            CurrentInstance.SetActive(false);
            Destroy(CurrentInstance);
            CurrentInstance = null;
        }

        CurrentLevelData = null;
    }

    private void HandleLevelLoaded(int _)
    {
        Load(LevelManager.Instance != null
            ? LevelManager.Instance.CurrentLevelData
            : null);
    }
}
