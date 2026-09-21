using UnityEngine;

/// <summary>
/// Base asset for game-specific level data.
/// Derive a focused type in each game and add only that game's fields.
/// </summary>
[CreateAssetMenu(fileName = "LevelData", menuName = "Template/Levels/Level Data")]
public class LevelData : ScriptableObject
{
    [SerializeField] private GameObject levelPrefab;

    public GameObject LevelPrefab => levelPrefab;
}
