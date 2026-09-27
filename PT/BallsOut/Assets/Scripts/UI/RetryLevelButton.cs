using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// HUD button that restarts the level in progress.
/// </summary>
[RequireComponent(typeof(Button))]
public sealed class RetryLevelButton : MonoBehaviour
{
    private Button button;

    private void Awake()
    {
        button = GetComponent<Button>();
    }

    private void OnEnable()
    {
        button.onClick.AddListener(Retry);
    }

    private void OnDisable()
    {
        button.onClick.RemoveListener(Retry);
    }

    private void Retry()
    {
        LevelManager manager = LevelManager.Instance;
        if (manager == null || manager.State != LevelState.Playing)
            return;

        GameHaptics.Light();
        manager.RetryLevel();
    }
}
