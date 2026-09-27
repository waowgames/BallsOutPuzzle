using UnityEditor;
using UnityEngine;

internal static class TutorialMenu
{
    private const string SaveKey = "template_save_v1";

    [MenuItem("Balls Out/Tutorial/Reset All Tutorials")]
    private static void ResetTutorials()
    {
        if (Application.isPlaying && SaveService.Instance != null)
        {
            SaveService.Instance.ResetTutorials();
            Debug.Log("[Tutorial] Tutorials reset. They play again from the next level start.");
            return;
        }

        // Outside play mode the save only lives in PlayerPrefs.
        string json = PlayerPrefs.GetString(SaveKey, string.Empty);
        if (string.IsNullOrEmpty(json)) return;
        var data = JsonUtility.FromJson<GameSaveData>(json);
        if (data == null) return;
        data.tutorials?.Clear();
        PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(data));
        PlayerPrefs.Save();
        Debug.Log("[Tutorial] Tutorials reset.");
    }
}
