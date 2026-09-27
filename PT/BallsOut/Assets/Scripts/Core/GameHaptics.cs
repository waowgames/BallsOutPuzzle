using Solo.MOST_IN_ONE;
using UnityEngine;

/// <summary>
/// Single entry point for haptics. Wraps MOST_HapticFeedback and honours
/// the player's vibration toggle from the settings panel.
/// </summary>
public static class GameHaptics
{
    // Rapid-fire feedback (ball intake, drag steps) is throttled so iOS never drops calls.
    public const float RapidCooldown = 0.06f;

    public static bool IsEnabled
    {
        get
        {
            SoundManager sound = SoundManager.Instance;
            return sound == null || sound.IsVibrationEnabled;
        }
    }

    public static void Play(MOST_HapticFeedback.HapticTypes type)
    {
        if (IsEnabled)
            MOST_HapticFeedback.Generate(type);
    }

    public static void PlayRapid(MOST_HapticFeedback.HapticTypes type, float cooldown = RapidCooldown)
    {
        if (IsEnabled)
            MOST_HapticFeedback.GenerateWithCooldown(type, cooldown);
    }

    public static void Selection() => PlayRapid(MOST_HapticFeedback.HapticTypes.Selection);
    public static void Light() => Play(MOST_HapticFeedback.HapticTypes.LightImpact);
    public static void Medium() => Play(MOST_HapticFeedback.HapticTypes.MediumImpact);
    public static void Heavy() => Play(MOST_HapticFeedback.HapticTypes.HeavyImpact);
    public static void Rigid() => Play(MOST_HapticFeedback.HapticTypes.RigidImpact);
    public static void Success() => Play(MOST_HapticFeedback.HapticTypes.Success);
    public static void Warning() => Play(MOST_HapticFeedback.HapticTypes.Warning);
    public static void Failure() => Play(MOST_HapticFeedback.HapticTypes.Failure);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void HookLevelEvents()
    {
        // GameEvents clears its listeners at SubsystemRegistration, which runs before this.
        GameEvents.OnLevelCompleted += _ => Success();
        GameEvents.OnLevelFailed += _ => Failure();
    }
}
