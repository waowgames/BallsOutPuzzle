using System;

/// <summary>
/// Implemented by a booster effect that needs the player to pick a target first (hammer, magnet).
/// The booster button spends the booster only when <paramref name="onCommitted"/> runs, so backing
/// out of the pick costs nothing.
/// </summary>
public interface ITargetedBooster
{
    /// <returns>False when picking could not start; nothing is spent then.</returns>
    bool BeginTargeting(Action onCommitted);
}
