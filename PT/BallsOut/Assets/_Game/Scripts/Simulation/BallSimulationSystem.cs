using System;
using System.Collections.Generic;
using UnityEngine;

namespace BallsOut
{
    public sealed class BallSimulationSystem
    {
        private readonly BallMicroGrid grid;
        private readonly BallCollectionSystem collection;
        private readonly List<BallState> moving = new List<BallState>();
        private readonly float visualHeight;
        private bool chooseLeft = true;
        private int observedRevision = -1;
        private float elapsed;
        private readonly Action<float> advanceBoxSystems;
        private readonly Func<bool> hasPendingBoxWork;
        public float TickInterval { get; }
        public float InterpolationTime => Mathf.Min(elapsed, TickInterval);
        public bool IsStable { get; private set; }
        public bool IsAnimating => moving.Count > 0;
        public long TickCount { get; private set; }
        public event Action<bool> OnStabilityChanged;

        public BallSimulationSystem(BallMicroGrid grid, BallCollectionSystem collection, float tickInterval, float visualHeight,
            Action<float> advanceBoxSystems, Func<bool> hasPendingBoxWork)
        {
            this.grid = grid;
            this.collection = collection;
            this.visualHeight = visualHeight;
            this.advanceBoxSystems = advanceBoxSystems;
            this.hasPendingBoxWork = hasPendingBoxWork;
            TickInterval = Mathf.Max(0.02f, tickInterval);
        }

        public void Advance(float deltaTime)
        {
            if (grid.Board.Revision != observedRevision)
            {
                observedRevision = grid.Board.Revision;
                SetStable(false);
            }
            if (IsStable && moving.Count == 0 && !hasPendingBoxWork()) return;
            // Bounded catch-up; keep the remainder so lag never changes tick ordering.
            elapsed += deltaTime;
            int budget = 4;
            while (elapsed >= TickInterval && budget-- > 0)
            {
                Interpolate(1f);
                foreach (var ball in moving) grid.ReserveVisual(ball.PreviousMacro, -1);
                moving.Clear();
                elapsed -= TickInterval;
                advanceBoxSystems(TickInterval);
                Tick();
                if (IsStable && !hasPendingBoxWork()) { elapsed = 0f; break; }
            }
            Interpolate(Mathf.Clamp01(elapsed / TickInterval));
        }

        private void Tick()
        {
            bool changed = false;
            TickCount++;
            // Bottom-up in-place traversal: destinations are in rows already visited,
            // so each ball is processed once without a second buffer or allocations.
            for (int y = grid.Board.Definition.lowerGridHeight * 3; y < grid.Height; y++)
                for (int x = 0; x < grid.Width; x++)
                {
                    BallState ball = grid.Get(new Vector2Int(x, y));
                    if (ball == null) continue;
                    BallMicroGrid.DownNeighbors(ball.Cell, out var down, out var left, out var right);
                    // Straight-down on odd-r is two rows. Both intervening sites
                    // must be empty; never jump a ball, wall, box or mask hole.
                    if (grid.IsEmpty(left) && grid.IsEmpty(right) && CanEnter(ball, down))
                    {
                        Enter(ball, down);
                        changed = true;
                        continue;
                    }
                    bool canLeft = CanEnter(ball, left);
                    bool canRight = CanEnter(ball, right);
                    if (!canLeft && !canRight) continue;
                    Vector2Int target = canLeft && canRight ? (chooseLeft ? left : right) : canLeft ? left : right;
                    if (canLeft && canRight) chooseLeft = !chooseLeft;
                    Enter(ball, target);
                    changed = true;
                }
            SetStable(!changed);
        }

        private bool CanEnter(BallState ball, Vector2Int cell) => grid.IsEmpty(cell) || collection.CanEnter(ball, cell);

        private void Enter(BallState ball, Vector2Int cell)
        {
            if (collection.TryCollect(ball, cell)) return;
            Vector3 from = grid.CellToLocal(ball.Cell) + Vector3.up * visualHeight;
            ball.PreviousMacro = BallMicroGrid.ToMacro(ball.Cell);
            grid.Move(ball, cell);
            ball.AnimationStart = from;
            ball.AnimationEnd = grid.CellToLocal(cell) + Vector3.up * visualHeight;
            // Reserve even without art: prefab assignment must not change movement legality.
            grid.ReserveVisual(ball.PreviousMacro, 1);
            moving.Add(ball);
        }

        private void Interpolate(float amount)
        {
            foreach (var ball in moving)
                if (ball.Visual != null)
                    ball.Visual.localPosition = Vector3.LerpUnclamped(ball.AnimationStart, ball.AnimationEnd, amount);
        }

        private void SetStable(bool stable)
        {
            if (IsStable == stable) return;
            IsStable = stable;
            OnStabilityChanged?.Invoke(stable);
        }
    }
}
