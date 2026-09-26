using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Swaps an Image's sprite to its Hard / Very Hard variant while such a level is loaded.
/// Leave a variant empty to keep the authored sprite for that difficulty.
/// </summary>
[RequireComponent(typeof(Image))]
public sealed class DifficultyThemeImage : MonoBehaviour
{
    [SerializeField] private Sprite hardSprite;
    [SerializeField] private Sprite veryHardSprite;

    private Image image;
    private Sprite normalSprite;

    private void Awake()
    {
        image = GetComponent<Image>();
        normalSprite = image.sprite;
    }

    private void OnEnable()
    {
        GameEvents.OnLevelLoaded += HandleLevelLoaded;

        if (LevelManager.Instance != null)
            Apply(LevelManager.Instance.CurrentDifficulty);
    }

    private void OnDisable()
    {
        GameEvents.OnLevelLoaded -= HandleLevelLoaded;
    }

    private void HandleLevelLoaded(int _)
    {
        Apply(LevelManager.Instance != null
            ? LevelManager.Instance.CurrentDifficulty
            : LevelDifficulty.Normal);
    }

    private void Apply(LevelDifficulty difficulty)
    {
        Sprite themed = difficulty == LevelDifficulty.Hard ? hardSprite
            : difficulty == LevelDifficulty.VeryHard ? veryHardSprite
            : null;
        image.sprite = themed != null ? themed : normalSprite;
    }
}
