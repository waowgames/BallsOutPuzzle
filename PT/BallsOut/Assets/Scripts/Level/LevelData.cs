using UnityEngine;

/// <summary>
/// Base asset for game-specific level data.
/// Derive a focused type in each game and add only that game's fields.
/// </summary>
[CreateAssetMenu(fileName = "LevelData", menuName = "Template/Levels/Level Data")]
public class LevelData : ScriptableObject
{
    [SerializeField] private GameObject levelPrefab;
    [Tooltip("Seconds the player has to finish the level. 0 = no time limit.")]
    [SerializeField, Min(0f)] private float timeLimitSeconds = 180f;
    [Tooltip("Hard / Very Hard levels get the red theme, the intro banner and the HUD badge.")]
    [SerializeField] private LevelDifficulty difficulty = LevelDifficulty.Normal;

    public GameObject LevelPrefab => levelPrefab;
    public float TimeLimitSeconds => timeLimitSeconds;
    public LevelDifficulty Difficulty => difficulty;
}
