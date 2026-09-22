using System;
using UnityEngine;

namespace BallsOut
{
    public sealed class BoxMovementSystem
    {
        private readonly BoardGrid board;
        private readonly BallMicroGrid balls;
        private readonly float stepDuration;
        private BoxController selected;
        private Vector3 grabOffset;
        private Vector3 stepStart;
        private Vector2Int stepOrigin;
        private Vector2Int target;
        private float stepElapsed;
        private bool releaseRequested;
        public bool IsDragging => selected != null;
        public event Action<BoxController> OnBoxMoved;
        public event Action<BoxController> OnDragEnded;

        public BoxMovementSystem(BoardGrid board, BallMicroGrid balls, float stepDuration)
        {
            this.board = board;
            this.balls = balls;
            this.stepDuration = Mathf.Max(0.02f, stepDuration);
        }

        public bool Begin(Vector3 worldPoint)
        {
            if (selected != null) return false;
            selected = board.GetBox(board.WorldToCell(worldPoint));
            if (selected == null || !selected.CanMove) { selected = null; return false; }
            grabOffset = board.Root.InverseTransformPoint(worldPoint) - board.CellToLocal(selected.Origin);
            target = selected.Origin;
            releaseRequested = false;
            return true;
        }

        public void Drag(Vector3 worldPoint)
        {
            if (selected == null || releaseRequested) return;
            Vector3 originPoint = board.Root.InverseTransformPoint(worldPoint) - grabOffset;
            target = new Vector2Int(Mathf.FloorToInt(originPoint.x / board.CellSize), Mathf.FloorToInt(originPoint.z / board.CellSize));
        }

        public void Release()
        {
            // Stop at the latest accepted neighboring origin. Never traverse an
            // unvisited route after release just because its endpoint is free.
            releaseRequested = true;
        }

        public void Advance(float deltaTime)
        {
            if (selected == null) return;
            if (!selected.CanMove) { Cancel(); return; }
            if (selected.IsInTransit)
            {
                stepElapsed += deltaTime;
                float t = Mathf.Clamp01(stepElapsed / stepDuration);
                selected.transform.localPosition = Vector3.Lerp(stepStart, board.CellToLocal(selected.Origin), t);
                if (t < 1f) return;
                board.FinishTransit(selected, stepOrigin);
                OnBoxMoved?.Invoke(selected);
            }
            if (releaseRequested)
            {
                BoxController ended = selected;
                selected = null;
                OnDragEnded?.Invoke(ended);
                return;
            }
            Vector2Int delta = target - selected.Origin;
            if (delta == Vector2Int.zero) return;
            Vector2Int horizontal = new Vector2Int(Math.Sign(delta.x), 0);
            Vector2Int vertical = new Vector2Int(0, Math.Sign(delta.y));
            if (Math.Abs(delta.x) >= Math.Abs(delta.y))
            {
                if (!TryStep(horizontal)) TryStep(vertical);
            }
            else if (!TryStep(vertical)) TryStep(horizontal);
        }

        private bool TryStep(Vector2Int step)
        {
            if (step == Vector2Int.zero) return false;
            Vector2Int next = selected.Origin + step;
            if (!board.CanPlace(selected, next, balls)) return false;
            stepOrigin = selected.Origin;
            stepStart = selected.transform.localPosition;
            stepElapsed = 0f;
            selected.IsInTransit = true;
            board.TryPlace(selected, next, balls, true);
            return true;
        }

        public void Cancel()
        {
            if (selected == null) return;
            if (selected.IsInTransit)
            {
                selected.transform.localPosition = board.CellToLocal(selected.Origin);
                board.FinishTransit(selected, stepOrigin);
            }
            selected = null;
            releaseRequested = false;
        }
    }
}
