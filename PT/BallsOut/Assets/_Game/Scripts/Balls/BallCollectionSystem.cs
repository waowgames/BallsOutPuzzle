using System;
using UnityEngine;

namespace BallsOut
{
    public sealed class BallCollectionSystem
    {
        private readonly BallMicroGrid grid;
        private readonly BoxFillSystem fill;
        private readonly BoxCompletionSystem completion;
        private readonly int firstBallRow;
        private readonly int collectionReachRows;
        private readonly int magnetMaxRemaining;
        private readonly int magnetReachRows;
        private readonly int magnetReachColumns;
        private const float MagnetStagger = 0.06f;
        public event Action<BallState, BoxController> OnBallCollected;

        public BallCollectionSystem(BallMicroGrid grid, BoxFillSystem fill, BoxCompletionSystem completion, int sinkReachRows,
            int magnetMaxRemaining = 0, int magnetReachRows = 0, int magnetReachColumns = 0)
        {
            this.grid = grid;
            this.fill = fill;
            this.completion = completion;
            firstBallRow = grid.Board.Definition.lowerGridHeight * LevelDefinition.MicroResolution;
            // The ordinary downward intake already reaches the lowest two rows.
            collectionReachRows = Mathf.Clamp(sinkReachRows, 2, 3);
            this.magnetMaxRemaining = Mathf.Max(0, magnetMaxRemaining);
            this.magnetReachRows = Mathf.Max(0, magnetReachRows);
            this.magnetReachColumns = Mathf.Max(0, magnetReachColumns);
        }

        // Called once the pile has settled: a box that needs only its last few balls pulls in the
        // matching ones stuck close above it (a few rows up, or a few columns off to the side).
        internal bool PullStragglers()
        {
            if (magnetMaxRemaining <= 0 || magnetReachRows <= 0) return false;
            int mouthRow = firstBallRow - 1;
            bool any = false;
            BoxController previous = null;
            for (int x = 0; x < grid.Width; x++)
            {
                BoxController box = MouthBox(x, mouthRow);
                // Each run of mouth columns under one box is handled once, from its left end.
                if (box == null || box == previous) { previous = box; continue; }
                previous = box;
                if (box.IsInTransit || !box.CanCollect(box.ActiveColor) || box.Capacity - box.CurrentFill > magnetMaxRemaining)
                    continue;
                int right = x;
                while (right + 1 < grid.Width && MouthBox(right + 1, mouthRow) == box) right++;
                any |= PullInto(box, x, right);
            }
            return any;
        }

        private BoxController MouthBox(int x, int mouthRow) => grid.Board.GetBox(BallMicroGrid.ToMacro(new Vector2Int(x, mouthRow)));

        private bool PullInto(BoxController box, int left, int right)
        {
            float delay = 0f;
            bool any = false;
            int top = Mathf.Min(grid.Height, firstBallRow + magnetReachRows);
            // Lowest rows first, so the pile barely shifts.
            for (int y = firstBallRow; y < top && box.CurrentFill < box.Capacity; y++)
                for (int x = left - magnetReachColumns; x <= right + magnetReachColumns && box.CurrentFill < box.Capacity; x++)
                {
                    BallState ball = grid.Get(new Vector2Int(x, y));
                    if (ball == null || ball.Color != box.ActiveColor) continue;
                    // Never across a reservoir divider.
                    if (!grid.CanTravel(ball.Cell, new Vector2Int(Mathf.Clamp(x, left, right), firstBallRow))) continue;
                    Collect(ball, box, delay);
                    delay += MagnetStagger;
                    any = true;
                }
            return any;
        }

        public bool CanEnter(BallState ball, Vector2Int destination)
        {
            int row = ball.Cell.y - firstBallRow;
            if (row < 0 || row >= collectionReachRows) return false;
            // Collection precedes the ball-area mask check: a box at the storage
            // boundary can accept balls, but an empty storage cell never can.
            BoxController box = grid.Board.GetBox(BallMicroGrid.ToMacro(destination));
            return box != null && box.CanCollect(ball.Color);
        }

        internal bool TryCollect(BallState ball, Vector2Int destination)
        {
            if (!CanEnter(ball, destination)) return false;
            Collect(ball, grid.Board.GetBox(BallMicroGrid.ToMacro(destination)), 0f);
            return true;
        }

        // Booster path: the ball flies straight into the box from anywhere in the pile.
        internal void Collect(BallState ball, BoxController box, float delay)
        {
            box.RecordCollection();
            grid.Remove(ball);
            fill.Collect(ball, box, delay);
            completion.Enqueue(box);
            box.NotifyCollection();
            OnBallCollected?.Invoke(ball, box);
        }
    }
}
