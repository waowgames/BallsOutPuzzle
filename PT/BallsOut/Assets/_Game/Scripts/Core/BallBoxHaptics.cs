using UnityEngine;

namespace BallsOut
{
    /// <summary>
    /// Maps gameplay events of the active level runtime to haptic feedback.
    /// Hooks itself up; no scene object is needed.
    /// </summary>
    public static class BallBoxHaptics
    {
        private static BallBoxLevelRuntime current;
        private static BoxMovementSystem movement;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Hook()
        {
            // BallBoxLevelRuntime resets its static event at SubsystemRegistration, before this runs.
            current = null;
            movement = null;
            BallBoxLevelRuntime.OnRuntimeLoaded += Attach;
        }

        private static void Attach(BallBoxLevelRuntime runtime)
        {
            Detach();
            current = runtime;
            movement = runtime.Movement;
            runtime.OnBallCollected += BallCollected;
            runtime.OnBoxCompleted += BoxCompleted;
            runtime.OnInnerLayerCompleted += InnerLayerCompleted;
            runtime.OnIceCracked += IceCracked;
            runtime.OnIceBroken += IceBroken;
            runtime.OnKeyDelivered += KeyDelivered;
            runtime.OnBoxUnlocked += BoxUnlocked;
            runtime.OnChainBroken += ChainBroken;
            runtime.OnObstacleCracked += ObstacleCracked;
            runtime.OnObstacleCleared += ObstacleCleared;
            runtime.OnBoxesFilledByBooster += BoosterEffect;
            runtime.OnBoxMagnetized += MagnetEffect;
            movement.OnDragBegan += DragBegan;
            movement.OnDragRefused += DragRefused;
            movement.OnBoxMoved += BoxMoved;
            movement.OnDragEnded += DragEnded;
        }

        private static void Detach()
        {
            if (current != null)
            {
                current.OnBallCollected -= BallCollected;
                current.OnBoxCompleted -= BoxCompleted;
                current.OnInnerLayerCompleted -= InnerLayerCompleted;
                current.OnIceCracked -= IceCracked;
                current.OnIceBroken -= IceBroken;
                current.OnKeyDelivered -= KeyDelivered;
                current.OnBoxUnlocked -= BoxUnlocked;
                current.OnChainBroken -= ChainBroken;
                current.OnObstacleCracked -= ObstacleCracked;
                current.OnObstacleCleared -= ObstacleCleared;
                current.OnBoxesFilledByBooster -= BoosterEffect;
                current.OnBoxMagnetized -= MagnetEffect;
            }
            if (movement != null)
            {
                movement.OnDragBegan -= DragBegan;
                movement.OnDragRefused -= DragRefused;
                movement.OnBoxMoved -= BoxMoved;
                movement.OnDragEnded -= DragEnded;
            }
            current = null;
            movement = null;
        }

        private static void DragBegan(BoxController _) => GameHaptics.Light();
        private static void DragRefused(BoxController _) => GameHaptics.Warning();
        private static void BoxMoved(BoxController _) => GameHaptics.Selection();
        private static void DragEnded(BoxController _) => GameHaptics.Selection();
        private static void BallCollected(BallState _, BoxController __) =>
            GameHaptics.PlayRapid(Solo.MOST_IN_ONE.MOST_HapticFeedback.HapticTypes.LightImpact, 0.08f);
        private static void BoxCompleted(BoxController _) => GameHaptics.Medium();
        private static void InnerLayerCompleted(BoxController _) => GameHaptics.Medium();
        private static void IceCracked(BoxController _) => GameHaptics.Light();
        private static void IceBroken(BoxController _) => GameHaptics.Heavy();
        private static void KeyDelivered(BoxController _) => GameHaptics.Light();
        private static void BoxUnlocked(BoxController _) => GameHaptics.Rigid();
        private static void ChainBroken(BoxController _) => GameHaptics.Rigid();
        private static void ObstacleCracked(BoardObstacle _) => GameHaptics.Medium();
        private static void ObstacleCleared(BoardObstacle _) => GameHaptics.Heavy();
        private static void BoosterEffect() => GameHaptics.Heavy();
        private static void MagnetEffect(BoxController _) => GameHaptics.Heavy();
    }
}
