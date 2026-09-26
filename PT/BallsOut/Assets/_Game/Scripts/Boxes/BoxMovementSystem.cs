using System;
using UnityEngine;

namespace BallsOut
{
    public sealed class BoxMovementSystem
    {
        // The dragged box and, when it is chained, its partner.
        private struct Pose
        {
            public Vector3 box;
            public Vector3 partner;
        }

        private readonly BoardGrid board;
        private readonly BallMicroGrid balls;
        private readonly float snapDuration;
        private BoxController selected;
        // The chained partner follows once the chain is taut and snaps home with the dragged box.
        private BoxController partner;
        private bool partnerTowed;
        private Vector3 partnerSnapStart;
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
            // A padlocked box stays put; the padlock shakes to say why.
            if (candidate != null && candidate.IsLocked) candidate.NudgeLock();
            selected = candidate;
            if (selected == null || !selected.CanMove) { selected = null; return false; }
            grabOffset = board.Root.InverseTransformPoint(worldPoint) - selected.transform.localPosition;
            releaseRequested = false;
            selected.IsInTransit = true;
            partner = selected.ChainPartner;
            partnerTowed = false;
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
            var current = new Pose
            {
                box = selected.transform.localPosition,
                partner = partner != null ? partner.transform.localPosition : Vector3.zero
            };
            Vector3 desired = board.Root.InverseTransformPoint(worldPoint) - grabOffset;
            desired.y = current.box.y;
            // An axis-locked box never leaves its row or column, so pin the other coordinate.
            if (selected.MoveAxis == BoxMoveAxis.Horizontal) desired.z = board.CellToLocal(selected.Origin).z;
            else if (selected.MoveAxis == BoxMoveAxis.Vertical) desired.x = board.CellToLocal(selected.Origin).x;
            Pose horizontalFirst = current;
            MoveAxis(ref horizontalFirst, desired.x, true, false);
            MoveAxis(ref horizontalFirst, desired.z, false, false);
            int route = 0;
            float remaining = (horizontalFirst.box - desired).sqrMagnitude;
            if (!Mathf.Approximately(horizontalFirst.box.x, desired.x) || !Mathf.Approximately(horizontalFirst.box.z, desired.z))
            {
                Pose verticalFirst = current;
                MoveAxis(ref verticalFirst, desired.z, false, false);
                MoveAxis(ref verticalFirst, desired.x, true, false);
                if ((verticalFirst.box - desired).sqrMagnitude < remaining)
                {
                    remaining = (verticalFirst.box - desired).sqrMagnitude;
                    route = 1;
                }
                Vector3 center = board.CellToLocal(selected.Origin);
                if (!Mathf.Approximately(current.box.z, center.z))
                {
                    Pose aligned = current;
                    MoveAxis(ref aligned, center.z, false, false);
                    MoveAxis(ref aligned, desired.x, true, false);
                    MoveAxis(ref aligned, desired.z, false, false);
                    if ((aligned.box - desired).sqrMagnitude < remaining)
                    {
                        remaining = (aligned.box - desired).sqrMagnitude;
                        route = 2;
                    }
                }
                if (!Mathf.Approximately(current.box.x, center.x))
                {
                    Pose aligned = current;
                    MoveAxis(ref aligned, center.x, true, false);
                    MoveAxis(ref aligned, desired.z, false, false);
                    MoveAxis(ref aligned, desired.x, true, false);
                    if ((aligned.box - desired).sqrMagnitude < remaining) route = 3;
                }
            }
            Pose pose = current;
            if (route == 2) MoveAxis(ref pose, board.CellToLocal(selected.Origin).z, false, true);
            if (route == 3) MoveAxis(ref pose, board.CellToLocal(selected.Origin).x, true, true);
            if (route == 0 || route == 2) MoveAxis(ref pose, desired.x, true, true);
            MoveAxis(ref pose, desired.z, false, true);
            if (route == 1 || route == 3) MoveAxis(ref pose, desired.x, true, true);
            selected.transform.localPosition = pose.box;
            if (partnerTowed) partner.transform.localPosition = pose.partner;
        }

        private void MoveAxis(ref Pose pose, float desired, bool horizontal, bool commit)
        {
            float start = horizontal ? pose.box.x : pose.box.z;
            int steps = Mathf.CeilToInt(Mathf.Abs(desired - start) * 4f / board.CellSize);
            BoxChain chain = partner != null && selected.ChainPartner == partner ? selected.Chain : null;
            for (int i = 1; i <= steps; i++)
            {
                Pose next = pose;
                float coordinate = Mathf.Lerp(start, desired, (float)i / steps);
                if (horizontal) next.box.x = coordinate;
                else next.box.z = coordinate;
                if (!CanOccupy(selected, next.box)) break;
                bool tows = false;
                if (chain != null)
                {
                    // Past the chain's reach the partner has to come along, or the drag stops here.
                    float pull = chain.Excess(selected, next.box, next.partner, horizontal);
                    if (pull != 0f)
                    {
                        if (horizontal) next.partner.x += pull;
                        else next.partner.z += pull;
                        if (!CanTow(horizontal) || !CanOccupy(partner, next.partner))
                        {
                            if (commit) Strain(chain);
                            break;
                        }
                        tows = true;
                    }
                }
                if (commit)
                {
                    Vector2Int origin = OriginOf(next.box);
                    Vector2Int partnerOrigin = tows ? OriginOf(next.partner) : default;
                    bool moves = origin != selected.Origin;
                    bool partnerMoves = tows && partnerOrigin != partner.Origin;
                    // Check both before either commits, so a blocked partner never strands the box.
                    if (moves && !board.CanPlace(selected, origin, balls)) break;
                    if (partnerMoves && !board.CanPlace(partner, partnerOrigin, balls)) break;
                    if (tows) BeginTow(chain);
                    if (moves)
                    {
                        board.TryPlace(selected, origin, balls);
                        OnBoxMoved?.Invoke(selected);
                    }
                    if (partnerMoves)
                    {
                        board.TryPlace(partner, partnerOrigin, balls);
                        OnBoxMoved?.Invoke(partner);
                    }
                }
                pose = next;
            }
        }

        private bool CanTow(bool horizontal) => partner.CanMove && (partner.MoveAxis == BoxMoveAxis.Free ||
            partner.MoveAxis == (horizontal ? BoxMoveAxis.Horizontal : BoxMoveAxis.Vertical));

        private void BeginTow(BoxChain chain)
        {
            if (partnerTowed) return;
            partnerTowed = true;
            partner.IsInTransit = true;
            chain.Tug();
            // A towed box collects like one the player picked up.
            if (!partner.HasPlayerInteracted)
            {
                partner.MarkPlayerInteraction();
                board.NotifyBoxStateChanged();
            }
        }

        // The chain holds against a box that cannot follow: it twangs, and a padlock shakes.
        private void Strain(BoxChain chain)
        {
            if (chain.Strain() && partner.IsLocked) partner.NudgeLock();
        }

        private Vector2Int OriginOf(Vector3 position) =>
            new Vector2Int(Mathf.FloorToInt(position.x / board.CellSize), Mathf.FloorToInt(position.z / board.CellSize));

        public void Release()
        {
            if (selected == null || releaseRequested) return;
            releaseRequested = true;
            snapStart = selected.transform.localPosition;
            if (partnerTowed) partnerSnapStart = partner.transform.localPosition;
            snapElapsed = 0f;
        }

        public void Advance(float deltaTime)
        {
            if (selected == null) return;
            // A box filled mid-drag lets go on its own so its completion or layer swap can play.
            if (selected.IsCompleting || selected.IsSwappingLayer ||
                partnerTowed && (partner.IsCompleting || partner.IsSwappingLayer)) Release();
            if (!releaseRequested) return;
            snapElapsed += deltaTime;
            float t = Mathf.Clamp01(snapElapsed / snapDuration);
            selected.transform.localPosition = Vector3.Lerp(snapStart, board.CellToLocal(selected.Origin), t);
            if (partnerTowed)
                partner.transform.localPosition = Vector3.Lerp(partnerSnapStart, board.CellToLocal(partner.Origin), t);
            if (t < 1f) return;
            board.FinishTransit(selected, selected.Origin);
            if (partnerTowed) board.FinishTransit(partner, partner.Origin);
            BoxController ended = selected;
            selected = null;
            partner = null;
            partnerTowed = false;
            OnDragEnded?.Invoke(ended);
        }

        private bool CanOccupy(BoxController box, Vector3 position)
        {
            float x = position.x / board.CellSize - 0.5f;
            float y = position.z / board.CellSize - 0.5f;
            // Box hit proxies occupy 95% of each grid cell; the remaining margin
            // lets a box slide along an adjacent wall without catching on it.
            int left = Mathf.FloorToInt(x + 0.025f);
            int right = Mathf.FloorToInt(x + 0.975f);
            int bottom = Mathf.FloorToInt(y + 0.025f);
            int top = Mathf.FloorToInt(y + 0.975f);
            if (!board.CanPlace(box, new Vector2Int(left, bottom), balls)) return false;
            if (right != left && !board.CanPlace(box, new Vector2Int(right, bottom), balls)) return false;
            if (top == bottom) return true;
            return board.CanPlace(box, new Vector2Int(left, top), balls) &&
                   (right == left || board.CanPlace(box, new Vector2Int(right, top), balls));
        }

        public void Cancel()
        {
            if (selected == null) return;
            selected.transform.localPosition = board.CellToLocal(selected.Origin);
            board.FinishTransit(selected, selected.Origin);
            if (partnerTowed)
            {
                partner.transform.localPosition = board.CellToLocal(partner.Origin);
                board.FinishTransit(partner, partner.Origin);
            }
            selected = null;
            partner = null;
            partnerTowed = false;
            releaseRequested = false;
        }
    }
}
