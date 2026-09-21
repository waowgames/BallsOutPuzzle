using TMPro;
using UnityEngine;

/// <summary>
/// Displays the active level number without polling the level manager.
/// </summary>
public sealed class LevelDisplayUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI levelText;
    [SerializeField] private string prefix = "LEVEL ";

    private void Awake()
    {
        if (levelText == null)
            levelText = GetComponent<TextMeshProUGUI>();
    }

    private void OnEnable()
    {
        GameEvents.OnLevelLoaded += HandleLevelLoaded;

        if (LevelManager.Instance != null)
            SetLevelNumber(LevelManager.Instance.DisplayedLevel1Based);
    }

    private void OnDisable()
    {
        GameEvents.OnLevelLoaded -= HandleLevelLoaded;
    }

    private void HandleLevelLoaded(int levelIndex)
    {
        SetLevelNumber(levelIndex + 1);
    }

    private void SetLevelNumber(int levelNumber)
    {
        if (levelText != null)
            levelText.text = $"{prefix}{Mathf.Max(1, levelNumber)}";
    }
}
