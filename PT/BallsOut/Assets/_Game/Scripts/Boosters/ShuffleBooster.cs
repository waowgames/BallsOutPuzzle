using UnityEngine;

namespace BallsOut
{
    /// <summary>
    /// Booster effect: reshuffles the ball pile so each open box finds its own colour right above it.
    /// Hook <see cref="Trigger"/> to a booster slot's onBoosterTriggered event.
    /// </summary>
    public sealed class ShuffleBooster : MonoBehaviour, IBoosterAvailability
    {
        public bool CanUseBooster()
        {
            BallBoxLevelRuntime runtime = BallBoxLevelRuntime.Active;
            return runtime != null && runtime.CanShuffle();
        }

        public void Trigger()
        {
            BallBoxLevelRuntime.Active?.ShuffleBalls();
        }
    }
}
