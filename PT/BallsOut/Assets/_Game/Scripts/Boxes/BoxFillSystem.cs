using System.Collections.Generic;
using UnityEngine;

namespace BallsOut
{
    public sealed class BoxFillSystem
    {
        private struct FillMotion
        {
            public BallState ball;
            public BoxController box;
            public Vector3 start;
            public Vector3 end;
            public float elapsed;
        }

        private readonly List<FillMotion> moving;
        private readonly BallPool pool;
        private readonly float duration;
        public bool IsAnimating => moving.Count != 0;

        public BoxFillSystem(BallPool pool, float duration, int ballCount)
        {
            this.pool = pool;
            this.duration = Mathf.Max(0.02f, duration);
            moving = new List<FillMotion>(ballCount);
        }

        public static Vector3 GetSlotPosition(BoxController box, int index)
        {
            Vector2Int cell = box.Shape.Cells[index / LevelDefinition.CapacityPerMacroCell];
            int slot = index % LevelDefinition.CapacityPerMacroCell;
            int layer = slot / 9;
            int row = slot % 9 / 3;
            int column = slot % 3;
            // Stable shape order; each cell fills 9 bottom, 9 middle, 9 top.
            return new Vector3(cell.x * box.runtimeCellSize, 0f, cell.y * box.runtimeCellSize)
                + Vector3.Scale(new Vector3(column - 1, layer, 1 - row), box.FillSpacing);
        }

        internal void Collect(BallState ball, BoxController box)
        {
            Vector3 end = GetSlotPosition(box, box.CurrentFill - 1);
            Vector3 start = end;
            if (ball.Visual != null)
            {
                // Parent once, then animate in the box's local space. Both settled
                // and arriving balls travel with a partially filled dragged box.
                ball.Visual.SetParent(box.FillRoot, true);
                start = ball.Visual.localPosition;
            }
            box.CollectedBalls.Add(ball);
            box.PendingFillAnimations++;
            moving.Add(new FillMotion { ball = ball, box = box, start = start, end = end });
        }

        public void Advance(float deltaTime)
        {
            for (int i = moving.Count - 1; i >= 0; i--)
            {
                FillMotion motion = moving[i];
                motion.elapsed += deltaTime;
                if (motion.elapsed < duration) { moving[i] = motion; continue; }
                if (motion.ball.Visual != null) motion.ball.Visual.localPosition = motion.end;
                motion.box.PendingFillAnimations--;
                int last = moving.Count - 1;
                moving[i] = moving[last];
                moving.RemoveAt(last);
            }
        }

        public void Render(float interpolationTime)
        {
            foreach (FillMotion motion in moving)
            {
                if (motion.ball.Visual == null) continue;
                float t = Mathf.Clamp01((motion.elapsed + interpolationTime) / duration);
                Vector3 position = Vector3.Lerp(motion.start, motion.end, t);
                position.y += Mathf.Sin(t * Mathf.PI) * motion.box.runtimeCellSize * 0.2f;
                motion.ball.Visual.localPosition = position;
            }
        }

        internal void Release(BoxController box)
        {
            foreach (BallState ball in box.CollectedBalls) pool.Return(ball);
            box.CollectedBalls.Clear();
        }

        internal void Clear(IReadOnlyList<BoxController> boxes)
        {
            moving.Clear();
            foreach (BoxController box in boxes)
            {
                if (box == null) continue;
                box.PendingFillAnimations = 0;
                Release(box);
            }
        }
    }
}
