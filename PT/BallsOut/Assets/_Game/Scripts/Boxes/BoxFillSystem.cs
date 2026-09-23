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
            public Vector3 startScale;
            public Vector3 endScale;
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
            int layerSize = box.FillSlots.Length;
            Vector3 position = box.FillSlots[index % layerSize];
            position.y = index / layerSize * box.FillSpacing.y;
            return position;
        }

        internal void Collect(BallState ball, BoxController box)
        {
            Vector3 end = GetSlotPosition(box, box.CurrentFill - 1);
            Vector3 start = end;
            Vector3 startScale = Vector3.one;
            Vector3 endScale = Vector3.one;
            if (ball.Visual != null)
            {
                // Parent once, then animate in the box's local space. Both settled
                // and arriving balls travel with a partially filled dragged box.
                ball.Visual.SetParent(box.FillRoot, true);
                start = ball.Visual.localPosition;
                startScale = ball.Visual.localScale;
                endScale = startScale * 0.65f;
            }
            box.CollectedBalls.Add(ball);
            box.PendingFillAnimations++;
            moving.Add(new FillMotion { ball = ball, box = box, start = start, end = end, startScale = startScale, endScale = endScale });
        }

        public void Advance(float deltaTime)
        {
            for (int i = moving.Count - 1; i >= 0; i--)
            {
                FillMotion motion = moving[i];
                motion.elapsed += deltaTime;
                if (motion.elapsed < duration) { moving[i] = motion; continue; }
                if (motion.ball.Visual != null)
                {
                    motion.ball.Visual.localPosition = motion.end;
                    motion.ball.Visual.localScale = motion.endScale;
                }
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
                t = 1f - (1f - t) * (1f - t) * (1f - t);
                motion.ball.Visual.localPosition = Vector3.Lerp(motion.start, motion.end, t);
                motion.ball.Visual.localScale = Vector3.Lerp(motion.startScale, motion.endScale, t);
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
