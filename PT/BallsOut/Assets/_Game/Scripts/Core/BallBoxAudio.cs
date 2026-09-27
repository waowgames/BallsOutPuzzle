using UnityEngine;

namespace BallsOut
{
    /// <summary>
    /// Maps gameplay events of the active level runtime to sound effects.
    /// Hooks itself up; no scene object is needed.
    /// </summary>
    public static class BallBoxAudio
    {
        // Each landing climbs a little in pitch so a filling box audibly "counts up".
        private const float FillPitchRise = 0.18f;
        private const float InnerLayerPitchOffset = -0.08f;

        private static BoxFillSystem fill;
        private static BoxCompletionSystem completion;
        private static BoxMovementSystem movement;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Hook()
        {
            // BallBoxLevelRuntime resets its static event at SubsystemRegistration, before this runs.
            fill = null;
            completion = null;
            movement = null;
            BallBoxLevelRuntime.OnRuntimeLoaded += Attach;
        }

        private static void Attach(BallBoxLevelRuntime runtime)
        {
            Detach();
            fill = runtime.Fill;
            completion = runtime.Completion;
            movement = runtime.Movement;
            fill.OnBallContact += BallContact;
            completion.OnCompletionAnimationStarted += CompletionStarted;
            completion.OnLayerSwapAnimationStarted += LayerSwapStarted;
            movement.OnDragBegan += DragBegan;
            movement.OnDragEnded += DragEnded;
        }

        private static void Detach()
        {
            if (fill != null) fill.OnBallContact -= BallContact;
            if (completion != null)
            {
                completion.OnCompletionAnimationStarted -= CompletionStarted;
                completion.OnLayerSwapAnimationStarted -= LayerSwapStarted;
            }
            if (movement != null)
            {
                movement.OnDragBegan -= DragBegan;
                movement.OnDragEnded -= DragEnded;
            }
            fill = null;
            completion = null;
            movement = null;
        }

        private static void Play(SoundId id, float pitchOffset = 0f) =>
            SoundManager.Instance?.PlaySfx(id, pitchOffset);

        private static void BallContact(BoxController box, int slot)
        {
            float progress = box.Capacity > 1 ? Mathf.Clamp01((float)slot / (box.Capacity - 1)) : 0f;
            Play(SoundId.BallIntoBox, progress * FillPitchRise);
        }

        private static void DragBegan(BoxController _) => Play(SoundId.BoxPickUp);
        private static void DragEnded(BoxController _) => Play(SoundId.BoxDrop);
        private static void CompletionStarted(BoxController _) => Play(SoundId.BoxComplete);
        private static void LayerSwapStarted(BoxController _) => Play(SoundId.BoxComplete, InnerLayerPitchOffset);
    }
}
