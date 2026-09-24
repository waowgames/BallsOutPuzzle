using UnityEditor;
using UnityEngine;

public static class LevelNavigationShortcuts
{
    [MenuItem("Tools/Levels/Next Level _k")]
    private static void NextLevel()
    {
        ChangeLevel(1);
    }

    [MenuItem("Tools/Levels/Previous Level _j")]
    private static void PreviousLevel()
    {
        ChangeLevel(-1);
    }

    private static void ChangeLevel(int offset)
    {
        if (!Application.isPlaying)
            return;

        LevelManager manager = LevelManager.Instance;
        if (manager == null)
            return;

        int index = Mathf.Max(0, manager.CurrentLevelIndex + offset);
        manager.LoadLevel(index);
        manager.StartLevel();
    }
}
