using System;
using UnityEngine;

namespace BallsOut
{
    public sealed class BallCollectionSystem
    {
        private readonly BallMicroGrid grid;
        private readonly BoxFillSystem fill;
        private readonly BoxCompletionSystem completion;
        public event Action<BallState, BoxController> OnBallCollected;

        public BallCollectionSystem(BallMicroGrid grid, BoxFillSystem fill, BoxCompletionSystem completion)
        {
            this.grid = grid;
            this.fill = fill;
            this.completion = completion;
        }

        public bool CanEnter(BallState ball, Vector2Int destination)
        {
            // Collection precedes the ball-area mask check: a box at the storage
            // boundary can accept balls, but an empty storage cell never can.
            BoxController box = grid.Board.GetBox(BallMicroGrid.ToMacro(destination));
            return box != null && box.CanCollect(ball.Color);
        }

        internal bool TryCollect(BallState ball, Vector2Int destination)
        {
            if (!CanEnter(ball, destination)) return false;
            BoxController box = grid.Board.GetBox(BallMicroGrid.ToMacro(destination));
            box.RecordCollection();
            grid.Remove(ball);
            fill.Collect(ball, box);
            completion.Enqueue(box);
            box.NotifyCollection();
            OnBallCollected?.Invoke(ball, box);
            return true;
        }
    }
}
