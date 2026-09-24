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

    public GameObject LevelPrefab => levelPrefab;
    public float TimeLimitSeconds => timeLimitSeconds;
}
