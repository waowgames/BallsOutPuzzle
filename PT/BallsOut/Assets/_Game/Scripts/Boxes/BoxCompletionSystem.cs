using System;
using System.Collections.Generic;
using UnityEngine;

namespace BallsOut
{
    public sealed class BoxCompletionSystem
    {
        private struct Completion
        {
            public BoxController box;
            public bool started;
            public float elapsed;
            public float duration;
        }

        private readonly BoardGrid board;
        private readonly BoxFillSystem fill;
        private readonly List<Completion> pending;
        private readonly float defaultDuration;
        public int RemainingBoxes { get; private set; }
        public bool IsAnimating => pending.Count != 0;
        public event Action<BoxController> OnBoxCompleted;
        public event Action<BoxController> OnBoxRemoved;

        public BoxCompletionSystem(BoardGrid board, BoxFillSystem fill, int boxCount, float defaultDuration)
        {
            this.board = board;
            this.fill = fill;
            this.defaultDuration = Mathf.Max(0f, defaultDuration);
            RemainingBoxes = boxCount;
            pending = new List<Completion>(boxCount);
        }

        internal void Enqueue(BoxController box)
        {
            if (box.CurrentFill != box.Capacity || box.IsCompleting || box.IsRemoved) return;
            box.IsCompleting = true;
            pending.Add(new Completion { box = box });
            OnBoxCompleted?.Invoke(box);
        }

        public void Advance(float deltaTime)
        {
            for (int i = pending.Count - 1; i >= 0; i--)
            {
                Completion entry = pending[i];
                BoxController box = entry.box;
                if (box.PendingFillAnimations != 0) continue;
                if (!entry.started)
                {
                    entry.started = true;
                    entry.duration = box.CompletionAnimation != null ? Mathf.Max(0f, box.CompletionAnimation.duration) : defaultDuration;
                    box.CompletionAnimation?.Begin();
                }
                else entry.elapsed += deltaTime;
                float t = entry.duration <= 0f ? 1f : Mathf.Clamp01(entry.elapsed / entry.duration);
                if (t < 1f) { pending[i] = entry; continue; }
                box.CompletionAnimation?.Finish();
                board.Remove(box);
                fill.Release(box);
                box.gameObject.SetActive(false);
                RemainingBoxes--;
                pending.RemoveAt(i);
                OnBoxRemoved?.Invoke(box);
            }
        }

        public void Render(float interpolationTime)
        {
            foreach (Completion entry in pending)
            {
                if (!entry.started || entry.box.CompletionAnimation != null) continue;
                float t = entry.duration <= 0f ? 1f : Mathf.Clamp01((entry.elapsed + interpolationTime) / entry.duration);
                entry.box.transform.localScale = Vector3.one * (1f - t * 0.9f);
            }
        }
    }
}
