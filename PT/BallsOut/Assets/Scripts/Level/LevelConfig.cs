using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Ordered level collection with optional looping after the final unique level.
/// </summary>
[CreateAssetMenu(fileName = "LevelConfig", menuName = "Template/Levels/Level Config")]
public sealed class LevelConfig : ScriptableObject
{
    [SerializeField] private List<LevelData> levels = new List<LevelData>();
    [SerializeField] private bool enableLooping = true;

    public IReadOnlyList<LevelData> Levels => levels;
    public bool EnableLooping => enableLooping;
    public int UniqueLevelCount => levels != null ? levels.Count : 0;

    public LevelData GetLevel(int absoluteIndex)
    {
        if (UniqueLevelCount == 0)
            return null;

        int safeIndex = Mathf.Max(0, absoluteIndex);
        if (safeIndex < UniqueLevelCount)
            return levels[safeIndex];

        return enableLooping
            ? levels[safeIndex % UniqueLevelCount]
            : levels[UniqueLevelCount - 1];
    }

    public int GetLoopCount(int absoluteIndex)
    {
        if (UniqueLevelCount == 0)
            return 0;

        return Mathf.Max(0, absoluteIndex) / UniqueLevelCount;
    }
}
