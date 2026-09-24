using UnityEngine;

/// <summary>
/// Booster effect: pauses the level countdown for a while.
/// Hook <see cref="Trigger"/> to a booster slot's onBoosterTriggered event.
/// </summary>
public sealed class TimeFreezeBooster : MonoBehaviour
{
    [SerializeField, Min(0f)] private float freezeSeconds = 15f;

    public void Trigger()
    {
        LevelTimer.Instance?.Freeze(freezeSeconds);
    }
}
