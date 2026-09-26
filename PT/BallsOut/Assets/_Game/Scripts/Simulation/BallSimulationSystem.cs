using System;
using System.Collections.Generic;
using UnityEngine;

namespace BallsOut
{
    public sealed class BallSimulationSystem
    {
        private readonly BallMicroGrid grid;
        private readonly BallCollectionSystem collection;
        private readonly BallFeederSystem feeder;
        private readonly Action<BallState, Vector3> launch;
        private readonly List<BallState> moving = new List<BallState>();
        private readonly float visualHeight;
        private readonly int sinkReachRows;
        private readonly int firstBallRow;
        private bool chooseLeft = true;
        private BallState slidRight;
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
            Action<float> advanceBoxSystems, Func<bool> hasPendingBoxWork, int sinkReachRows = 0, BallFeederSystem feeder = null)
        {
            this.grid = grid;
            this.collection = collection;
            this.feeder = feeder;
            launch = Launch;
            this.visualHeight = visualHeight;
            this.sinkReachRows = Mathf.Clamp(sinkReachRows, 0, 3);
            firstBallRow = grid.Board.Definition.lowerGridHeight * LevelDefinition.MicroResolution;
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
            slidRight = null;
            // Bottom-up in-place traversal: destinations are in rows already visited,
            // so each ball is processed once without a second buffer or allocations.
            for (int y = firstBallRow; y < grid.Height; y++)
                for (int x = 0; x < grid.Width; x++)
                {
                    BallState ball = grid.Get(new Vector2Int(x, y));
                    // A ball that rolled right this tick is met again at its new site.
                    if (ball == null || ball == slidRight) continue;
                    BallMicroGrid.DownNeighbors(ball.Cell, out var down, out var left, out var right);
                    // A box also draws from the second row: its matching ball
                    // hops over the front ball straight into the box.
                    if (collection.CanEnter(ball, down))
                    {
                        Enter(ball, down);
                        changed = true;
                        continue;
                    }
                    // Straight-down on odd-r is two rows. Both intervening sites
                    // must be empty; never jump a ball, wall, box or mask hole.
                    if (grid.IsEmpty(left) && grid.IsEmpty(right) && CanEnter(ball, down))
                    {
                        Enter(ball, down);
                        changed = true;
                        continue;
                    }
                    bool canLeft = CanEnter(ball, left);
                    bool canRight = CanEnter(ball, right) && !DefersTo(ball, right);
                    if (!canLeft && !canRight)
                    {
                        if (TrySlideOffWall(ball, left, right) || TryPull(ball)) changed = true;
                        continue;
                    }
                    Vector2Int target = canLeft && canRight ? (PrefersRight(right) ? right : left) : canLeft ? left : right;
                    Enter(ball, target);
                    changed = true;
                }
            // Tubes top up the row the pile has just left.
            if (feeder != null && feeder.Feed(launch)) changed = true;
            SetStable(!changed);
        }

        private bool CanEnter(BallState ball, Vector2Int cell) => grid.CanTravel(ball.Cell, cell) &&
            (grid.IsEmpty(cell) || collection.CanEnter(ball, cell));

        // Gravity: a hole is refilled from alternating sides row by row (even rows
        // from upper-left, odd rows from upper-right), so it climbs straight up its
        // column and the pile above drops vertically instead of shearing diagonally.
        private static bool PrefersRight(Vector2Int target) => (target.y & 1) == 0;

        // An odd-row hole belongs to the ball at its upper-right, which this row's
        // left-to-right sweep has not reached yet; the upper-left ball waits for it.
        private bool DefersTo(BallState ball, Vector2Int target)
        {
            if (PrefersRight(target) || !grid.IsEmpty(target)) return false;
            BallState owner = grid.Get(ball.Cell + Vector2Int.right);
            return owner != null && owner != slidRight;
        }

        // Funnel walls narrow faster than half a column per row, so a ball on the
        // wall can have no site below it at all. It rolls sideways along the slope
        // onto a site that does lead down; flat floors never qualify.
        private bool TrySlideOffWall(BallState ball, Vector2Int left, Vector2Int right)
        {
            if (grid.IsBallCell(left) || grid.IsBallCell(right)) return false;
            Vector2Int toLeft = ball.Cell + Vector2Int.left;
            Vector2Int toRight = ball.Cell + Vector2Int.right;
            bool canLeft = grid.CanTravel(ball.Cell, toLeft) && grid.IsEmpty(toLeft) && LeadsDown(toLeft);
            bool canRight = grid.CanTravel(ball.Cell, toRight) && grid.IsEmpty(toRight) && LeadsDown(toRight);
            if (!canLeft && !canRight) return false;
            Vector2Int target = canLeft && canRight ? (chooseLeft ? toLeft : toRight) : canLeft ? toLeft : toRight;
            if (canLeft && canRight) chooseLeft = !chooseLeft;
            if (target == toRight) slidRight = ball;
            Enter(ball, target);
            return true;
        }

        // A box reaches a few rows into the pile resting on it: a matching ball in the
        // lowest sinkReachRows, directly above the box, flies straight in, so one wrong
        // colour at the bottom no longer seals the box off. Nothing else is displaced;
        // the pile above follows down by ordinary falling.
        private bool TryPull(BallState ball)
        {
            if (sinkReachRows <= 0 || ball.Cell.y - firstBallRow >= sinkReachRows) return false;
            return collection.TryCollect(ball, new Vector2Int(ball.Cell.x, firstBallRow - 1));
        }

        private bool LeadsDown(Vector2Int cell)
        {
            BallMicroGrid.DownNeighbors(cell, out _, out var left, out var right);
            return grid.IsBallCell(left) || grid.IsBallCell(right);
        }

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

        // A ball dropped from a feeder tube falls from its place in the tube to its reservoir site.
        private void Launch(BallState ball, Vector3 from)
        {
            ball.PreviousMacro = BallMicroGrid.ToMacro(ball.Cell);
            ball.AnimationStart = from;
            ball.AnimationEnd = grid.CellToLocal(ball.Cell) + Vector3.up * visualHeight;
            grid.ReserveVisual(ball.PreviousMacro, 1);
            moving.Add(ball);
        }

        private void Interpolate(float amount)
        {
            feeder?.Render(amount);
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
