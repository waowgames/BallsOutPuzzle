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
            public float waited;
            public float elapsed;
            public float duration;
            public BoxCompletionEffect effect;
        }

        private struct LayerSwap
        {
            public BoxController box;
            public float waited;
            public float elapsed;
            public bool started;
            public InnerLayerSwapEffect effect;
        }

        private struct Smashing
        {
            public BoxController box;
            public float elapsed;
            public BoxSmashEffect effect;
        }

        // Time for a finished inner tray to lift, lid up and vanish; quicker than a full completion.
        private const float LayerSwapDuration = 0.7f;
        private const float SmashDuration = 0.45f;

        private readonly BoardGrid board;
        private readonly BoxFillSystem fill;
        private readonly List<Completion> pending;
        private readonly List<LayerSwap> swaps = new List<LayerSwap>();
        private readonly List<Smashing> smashes = new List<Smashing>();
        private readonly float defaultDuration;
        private readonly float startDelay;
        public int RemainingBoxes { get; private set; }
        public bool IsAnimating => pending.Count != 0 || swaps.Count != 0 || smashes.Count != 0;
        public event Action<BoxController> OnBoxCompleted;
        // A nested box filled its inner layer; it keeps going in its outer color.
        public event Action<BoxController> OnInnerLayerCompleted;
        public event Action<BoxController> OnBoxRemoved;
        // Fire when the finish animation actually begins, after the last ball has landed.
        public event Action<BoxController> OnCompletionAnimationStarted;
        public event Action<BoxController> OnLayerSwapAnimationStarted;

        public BoxCompletionSystem(BoardGrid board, BoxFillSystem fill, int boxCount, float defaultDuration, float startDelay)
        {
            this.board = board;
            this.fill = fill;
            this.defaultDuration = Mathf.Max(0f, defaultDuration);
            this.startDelay = Mathf.Max(0f, startDelay);
            RemainingBoxes = boxCount;
            pending = new List<Completion>(boxCount);
        }

        internal void Enqueue(BoxController box)
        {
            if (box.CurrentFill != box.Capacity || box.IsCompleting || box.IsSwappingLayer || box.IsRemoved) return;
            if (box.HasInnerLayer)
            {
                // Not a completion: no ice cracks and the box stays on the board.
                box.IsSwappingLayer = true;
                swaps.Add(new LayerSwap { box = box });
                OnInnerLayerCompleted?.Invoke(box);
                return;
            }
            box.IsCompleting = true;
            pending.Add(new Completion { box = box });
            OnBoxCompleted?.Invoke(box);
        }

        // Hammer booster: the box counts as completed at once and breaks apart instead of celebrating.
        internal void Smash(BoxController box)
        {
            if (box.IsCompleting || box.IsRemoved) return;
            box.IsCompleting = true;
            board.Remove(box);
            smashes.Add(new Smashing { box = box, effect = new BoxSmashEffect(box, SmashDuration) });
            OnBoxCompleted?.Invoke(box);
        }

        public void Advance(float deltaTime)
        {
            AdvanceSwaps(deltaTime);
            AdvanceSmashes(deltaTime);
            for (int i = pending.Count - 1; i >= 0; i--)
            {
                Completion entry = pending[i];
                BoxController box = entry.box;
                if (box.PendingFillAnimations != 0 || box.IsInTransit) continue;
                // Full boxes linger briefly so the player sees them filled before they vanish.
                if (entry.waited < startDelay)
                {
                    entry.waited += deltaTime;
                    pending[i] = entry;
                    continue;
                }
                if (!entry.started)
                {
                    entry.started = true;
                    entry.duration = box.CompletionAnimation != null ? Mathf.Max(0f, box.CompletionAnimation.duration) : defaultDuration;
                    if (box.CompletionAnimation != null) box.CompletionAnimation.Begin();
                    else entry.effect = new BoxCompletionEffect(box, entry.duration);
                    // Free the footprint now so play flows on: balls can drop in and another box
                    // can be dragged onto these cells while the finished box lifts away above them.
                    board.Remove(box);
                    OnCompletionAnimationStarted?.Invoke(box);
                }
                else entry.elapsed += deltaTime;
                float t = entry.duration <= 0f ? 1f : Mathf.Clamp01(entry.elapsed / entry.duration);
                if (t < 1f) { pending[i] = entry; continue; }
                box.CompletionAnimation?.Finish();
                entry.effect?.Finish();
                fill.Release(box);
                box.gameObject.SetActive(false);
                RemainingBoxes--;
                pending.RemoveAt(i);
                OnBoxRemoved?.Invoke(box);
            }
        }

        private void AdvanceSmashes(float deltaTime)
        {
            for (int i = smashes.Count - 1; i >= 0; i--)
            {
                Smashing smash = smashes[i];
                smash.elapsed += deltaTime;
                // Balls still flying in are pooled along with the box once they land.
                if (smash.elapsed < SmashDuration || smash.box.PendingFillAnimations != 0) { smashes[i] = smash; continue; }
                smash.effect.Finish();
                fill.Release(smash.box);
                smash.box.gameObject.SetActive(false);
                RemainingBoxes--;
                smashes.RemoveAt(i);
                OnBoxRemoved?.Invoke(smash.box);
            }
        }

        private void AdvanceSwaps(float deltaTime)
        {
            for (int i = swaps.Count - 1; i >= 0; i--)
            {
                LayerSwap swap = swaps[i];
                BoxController box = swap.box;
                if (box.PendingFillAnimations != 0 || box.IsInTransit) continue;
                if (swap.waited < startDelay)
                {
                    swap.waited += deltaTime;
                    swaps[i] = swap;
                    continue;
                }
                if (swap.started) swap.elapsed += deltaTime;
                else
                {
                    if (box.InnerArt != null) swap.effect = new InnerLayerSwapEffect(box, LayerSwapDuration);
                    OnLayerSwapAnimationStarted?.Invoke(box);
                }
                swap.started = true;
                if (swap.elapsed < LayerSwapDuration) { swaps[i] = swap; continue; }
                fill.Release(box);
                box.FinishInnerLayer();
                swaps.RemoveAt(i);
                // Wakes the ball simulation so outer-color balls can drop in.
                board.NotifyBoxStateChanged();
            }
        }

        public void Render(float interpolationTime)
        {
            foreach (Smashing smash in smashes)
                smash.effect.Evaluate((smash.elapsed + interpolationTime) / SmashDuration);
            foreach (LayerSwap swap in swaps)
            {
                if (!swap.started || swap.effect == null) continue;
                swap.effect.Evaluate((swap.elapsed + interpolationTime) / LayerSwapDuration);
            }
            foreach (Completion entry in pending)
            {
                if (entry.effect == null) continue;
                float t = entry.duration <= 0f ? 1f : Mathf.Clamp01((entry.elapsed + interpolationTime) / entry.duration);
                entry.effect.Evaluate(t);
            }
        }
    }
}
