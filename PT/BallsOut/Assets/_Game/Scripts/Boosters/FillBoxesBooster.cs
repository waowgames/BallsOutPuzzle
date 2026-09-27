using UnityEngine;

namespace BallsOut
{
    /// <summary>
    /// Booster effect: matching balls fly out of the pile straight into the boxes, fullest box first.
    /// Hook <see cref="Trigger"/> to a booster slot's onBoosterTriggered event.
    /// </summary>
    public sealed class FillBoxesBooster : MonoBehaviour, IBoosterAvailability
    {
        [Tooltip("How many boxes one use fills.")]
        [SerializeField, Min(1)] private int boxesPerUse = 2;

        public bool CanUseBooster()
        {
            BallBoxLevelRuntime runtime = BallBoxLevelRuntime.Active;
            return runtime != null && runtime.CanFillBoxes();
        }

        public void Trigger()
        {
            BallBoxLevelRuntime.Active?.FillBoxes(boxesPerUse);
        }
    }
}
