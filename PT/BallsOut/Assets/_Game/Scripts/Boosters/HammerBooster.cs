using System;
using UnityEngine;

namespace BallsOut
{
    /// <summary>
    /// Booster effect: the player picks a box, a hammer swings down on it and smashes it for good.
    /// The box counts as completed (ice, keys, chains and stones react as usual) and the balls it
    /// still needed pop out of play with it. Sits on the booster button; the button spends the
    /// booster only once a box is picked.
    /// </summary>
    public sealed class HammerBooster : MonoBehaviour, IBoosterAvailability, ITargetedBooster
    {
        [SerializeField] private GameObject hammerModel;
        [Tooltip("Hammer head size, in board cells.")]
        [SerializeField, Min(0.1f)] private float hammerSize = 1f;
        [SerializeField] private string prompt = "Tap a box to smash it!";
        [SerializeField] private Color outlineColor = new Color(1f, 0.82f, 0.2f);

        public bool CanUseBooster()
        {
            BallBoxLevelRuntime runtime = BallBoxLevelRuntime.Active;
            return runtime != null && runtime.CanSmashAny() && BoosterTargetingUI.Instance != null &&
                   !BoosterTargetingUI.Instance.IsPicking;
        }

        public bool BeginTargeting(Action onCommitted)
        {
            BallBoxLevelRuntime runtime = BallBoxLevelRuntime.Active;
            BoosterTargetingUI picker = BoosterTargetingUI.Instance;
            if (runtime == null || picker == null) return false;
            runtime.SetBoosterPaused(true);
            bool started = picker.Begin(prompt, outlineColor, runtime.CanSmash,
                box => Strike(runtime, box, onCommitted),
                () => runtime.SetBoosterPaused(false));
            if (!started) runtime.SetBoosterPaused(false);
            return started;
        }

        private void Strike(BallBoxLevelRuntime runtime, BoxController box, Action onCommitted)
        {
            onCommitted?.Invoke();
            // The board stays still until the head lands, so the box cannot change under the swing.
            HammerStrikeEffect.Play(hammerModel, box, hammerSize, () =>
            {
                if (runtime == null) return;
                runtime.SetBoosterPaused(false);
                runtime.SmashBox(box);
            });
        }
    }
}
