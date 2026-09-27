using System;
using UnityEngine;

namespace BallsOut
{
    /// <summary>
    /// Booster effect: the player picks a box and every ball of its colour in the pile, wherever it
    /// sits, flies into it until it is full. Locked and frozen boxes cannot be picked. Sits on the
    /// booster button; the button spends the booster only once a box is picked.
    /// </summary>
    public sealed class MagnetBooster : MonoBehaviour, IBoosterAvailability, ITargetedBooster
    {
        [SerializeField] private Sprite magnetSprite;
        [SerializeField] private string prompt = "Tap a box to pull in its balls!";
        [SerializeField] private Color outlineColor = new Color(0.45f, 0.9f, 1f);

        public bool CanUseBooster()
        {
            BallBoxLevelRuntime runtime = BallBoxLevelRuntime.Active;
            return runtime != null && runtime.CanMagnetizeAny() && BoosterTargetingUI.Instance != null &&
                   !BoosterTargetingUI.Instance.IsPicking;
        }

        public bool BeginTargeting(Action onCommitted)
        {
            BallBoxLevelRuntime runtime = BallBoxLevelRuntime.Active;
            BoosterTargetingUI picker = BoosterTargetingUI.Instance;
            if (runtime == null || picker == null) return false;
            runtime.SetBoosterPaused(true);
            bool started = picker.Begin(prompt, outlineColor, runtime.CanMagnetize,
                box =>
                {
                    onCommitted?.Invoke();
                    runtime.SetBoosterPaused(false);
                    runtime.MagnetizeBox(box);
                    MagnetPullEffect.Play(magnetSprite, box);
                },
                () => runtime.SetBoosterPaused(false));
            if (!started) runtime.SetBoosterPaused(false);
            return started;
        }
    }
}
