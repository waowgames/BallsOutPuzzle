using UnityEngine;

[CreateAssetMenu(
    fileName = "LevelRewardConfig",
    menuName = "Template/Economy/Level Reward Config")]
public sealed class LevelRewardConfig : ScriptableObject
{
    [SerializeField, Min(0)] private int baseReward = 3;
    [SerializeField, Min(0)] private int rewardPerLevel = 3;
    [SerializeField, Min(0)] private int maximumReward;

    public int GetReward(int completedLevelIndex)
    {
        long reward = baseReward +
            (long)Mathf.Max(0, completedLevelIndex) * rewardPerLevel;

        if (maximumReward > 0)
            reward = System.Math.Min(reward, maximumReward);

        return (int)System.Math.Min(reward, int.MaxValue);
    }
}
