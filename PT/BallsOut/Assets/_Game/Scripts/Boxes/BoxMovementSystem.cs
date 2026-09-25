using System;
using UnityEngine;

namespace BallsOut
{
    public sealed class BoxMovementSystem
    {
        private readonly BoardGrid board;
        private readonly BallMicroGrid balls;
        private readonly float snapDuration;
        private BoxController selected;
        private Vector3 grabOffset;
        private Vector3 snapStart;
        private float snapElapsed;
        private bool releaseRequested;
        public bool IsDragging => selected != null;
        public event Action<BoxController> OnBoxMoved;
        public event Action<BoxController> OnDragEnded;

        public BoxMovementSystem(BoardGrid board, BallMicroGrid balls, float snapDuration)
        {
            this.board = board;
            this.balls = balls;
            this.snapDuration = Mathf.Max(0.02f, snapDuration);
        }

        public bool Begin(Vector3 worldPoint, BoxController candidate)
        {
            if (selected != null) return false;
            selected = candidate;
            if (selected == null || !selected.CanMove) { selected = null; return false; }
            grabOffset = board.Root.InverseTransformPoint(worldPoint) - selected.transform.localPosition;
            releaseRequested = false;
            selected.IsInTransit = true;
            if (!selected.HasPlayerInteracted)
            {
                selected.MarkPlayerInteraction();
                board.NotifyBoxStateChanged();
            }
            return true;
        }

        public void Drag(Vector3 worldPoint)
        {
            if (selected == null || releaseRequested) return;
            Vector3 current = selected.transform.localPosition;
            Vector3 desired = board.Root.InverseTransformPoint(worldPoint) - grabOffset;
            desired.y = current.y;
            // An axis-locked box never leaves its row or column, so pin the other coordinate.
            if (selected.MoveAxis == BoxMoveAxis.Horizontal) desired.z = board.CellToLocal(selected.Origin).z;
            else if (selected.MoveAxis == BoxMoveAxis.Vertical) desired.x = board.CellToLocal(selected.Origin).x;
            Vector3 horizontalFirst = current;
            MoveAxis(ref horizontalFirst, desired.x, true, false);
            MoveAxis(ref horizontalFirst, desired.z, false, false);
            int route = 0;
            float remaining = (horizontalFirst - desired).sqrMagnitude;
            if (!Mathf.Approximately(horizontalFirst.x, desired.x) || !Mathf.Approximately(horizontalFirst.z, desired.z))
            {
                Vector3 verticalFirst = current;
                MoveAxis(ref verticalFirst, desired.z, false, false);
                MoveAxis(ref verticalFirst, desired.x, true, false);
                if ((verticalFirst - desired).sqrMagnitude < remaining)
                {
                    remaining = (verticalFirst - desired).sqrMagnitude;
                    route = 1;
                }
                Vector3 center = board.CellToLocal(selected.Origin);
                if (!Mathf.Approximately(current.z, center.z))
                {
                    Vector3 aligned = current;
                    MoveAxis(ref aligned, center.z, false, false);
                    MoveAxis(ref aligned, desired.x, true, false);
                    MoveAxis(ref aligned, desired.z, false, false);
                    if ((aligned - desired).sqrMagnitude < remaining)
                    {
                        remaining = (aligned - desired).sqrMagnitude;
                        route = 2;
                    }
                }
                if (!Mathf.Approximately(current.x, center.x))
                {
                    Vector3 aligned = current;
                    MoveAxis(ref aligned, center.x, true, false);
                    MoveAxis(ref aligned, desired.z, false, false);
                    MoveAxis(ref aligned, desired.x, true, false);
                    if ((aligned - desired).sqrMagnitude < remaining) route = 3;
                }
            }
            Vector3 position = current;
            if (route == 2) MoveAxis(ref position, board.CellToLocal(selected.Origin).z, false, true);
            if (route == 3) MoveAxis(ref position, board.CellToLocal(selected.Origin).x, true, true);
            if (route == 0 || route == 2) MoveAxis(ref position, desired.x, true, true);
            MoveAxis(ref position, desired.z, false, true);
            if (route == 1 || route == 3) MoveAxis(ref position, desired.x, true, true);
            selected.transform.localPosition = position;
        }

        private void MoveAxis(ref Vector3 position, float desired, bool horizontal, bool commit)
        {
            float start = horizontal ? position.x : position.z;
            int steps = Mathf.CeilToInt(Mathf.Abs(desired - start) * 4f / board.CellSize);
            for (int i = 1; i <= steps; i++)
            {
                Vector3 next = position;
                float coordinate = Mathf.Lerp(start, desired, (float)i / steps);
                if (horizontal) next.x = coordinate;
                else next.z = coordinate;
                if (!CanOccupy(next)) break;
                if (commit)
                {
                    Vector2Int origin = new Vector2Int(Mathf.FloorToInt(next.x / board.CellSize),
                        Mathf.FloorToInt(next.z / board.CellSize));
                    if (origin != selected.Origin)
                    {
                        if (!board.TryPlace(selected, origin, balls)) break;
                        OnBoxMoved?.Invoke(selected);
                    }
                }
                position = next;
            }
        }

        public void Release()
        {
            if (selected == null || releaseRequested) return;
            releaseRequested = true;
            snapStart = selected.transform.localPosition;
            snapElapsed = 0f;
        }

        public void Advance(float deltaTime)
        {
            if (selected == null) return;
            // A box filled mid-drag lets go on its own so its completion or layer swap can play.
            if (selected.IsCompleting || selected.IsSwappingLayer) Release();
            if (!releaseRequested) return;
            snapElapsed += deltaTime;
            float t = Mathf.Clamp01(snapElapsed / snapDuration);
            selected.transform.localPosition = Vector3.Lerp(snapStart, board.CellToLocal(selected.Origin), t);
            if (t < 1f) return;
            board.FinishTransit(selected, selected.Origin);
            BoxController ended = selected;
            selected = null;
            OnDragEnded?.Invoke(ended);
        }

        private bool CanOccupy(Vector3 position)
        {
            float x = position.x / board.CellSize - 0.5f;
            float y = position.z / board.CellSize - 0.5f;
            // Box hit proxies occupy 95% of each grid cell; the remaining margin
            // lets a box slide along an adjacent wall without catching on it.
            int left = Mathf.FloorToInt(x + 0.025f);
            int right = Mathf.FloorToInt(x + 0.975f);
            int bottom = Mathf.FloorToInt(y + 0.025f);
            int top = Mathf.FloorToInt(y + 0.975f);
            if (!board.CanPlace(selected, new Vector2Int(left, bottom), balls)) return false;
            if (right != left && !board.CanPlace(selected, new Vector2Int(right, bottom), balls)) return false;
            if (top == bottom) return true;
            return board.CanPlace(selected, new Vector2Int(left, top), balls) &&
                   (right == left || board.CanPlace(selected, new Vector2Int(right, top), balls));
        }

        public void Cancel()
        {
            if (selected == null) return;
            selected.transform.localPosition = board.CellToLocal(selected.Origin);
            board.FinishTransit(selected, selected.Origin);
            selected = null;
            releaseRequested = false;
        }
    }
}
