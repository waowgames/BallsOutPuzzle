/// <summary>
/// Implemented by a booster effect on the booster button to veto a use that would do
/// nothing, so the player never spends a booster for no effect.
/// </summary>
public interface IBoosterAvailability
{
    bool CanUseBooster();
}
