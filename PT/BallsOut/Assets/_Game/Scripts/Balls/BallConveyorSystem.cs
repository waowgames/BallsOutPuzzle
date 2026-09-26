using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace BallsOut
{
    // The conveyor over the reservoir. Its queue rides the track (ConveyorTrack) in order, head at the
    // gate. Whenever a top-row site under the gate is free, the head ball drops into it and the whole
    // train rolls up behind it; the balls that do not fit on the track yet come in through the chute.
    // A counter on the chute's cap shows everything still on the belt.
    public sealed class BallConveyorSystem
    {
        private const int R = LevelDefinition.MicroResolution;
        private readonly BallMicroGrid grid;
        private readonly BallPool pool;
        private readonly int column;
        private readonly int topRow;
        private readonly List<BallColorDefinition> queue = new List<BallColorDefinition>();
        private readonly List<Vector3> sites;
        // visuals[i] shows queue[next + i]; it rolls from starts[i] to sites[i]. Balls that have just
        // come in through the chute grow to their full scale meanwhile.
        private readonly List<Transform> visuals = new List<Transform>();
        private readonly List<Vector3> starts = new List<Vector3>();
        private readonly List<Vector3> scales = new List<Vector3>();
        private readonly List<bool> entering = new List<bool>();
        private readonly TextMeshPro counter;
        private int next;
        private bool rolling;
        public int Remaining => queue.Count - next;

        public BallConveyorSystem(BallMicroGrid grid, BallPool pool, Transform parent, float visualHeight)
        {
            this.grid = grid;
            this.pool = pool;
            LevelDefinition level = grid.Board.Definition;
            topRow = grid.Height - 1;
            if (!level.HasConveyor) return;
            column = level.conveyor.column;
            foreach (FeederSegment segment in level.conveyor.queue)
                for (int i = 0; i < segment.count; i++) queue.Add(segment.color);
            sites = ConveyorTrack.Sites(level, visualHeight);
            float s = level.macroCellSize;
            counter = BallFeederSystem.CreateCounter(parent, s, "Conveyor Counter",
                new Vector3((ConveyorTrack.ChuteColumn(level) + 0.5f) * s, s * 0.36f, ConveyorTrack.ChuteTop(level) + s * 0.13f));
            Restack(false);
            UpdateCounter();
        }

        // Called once per simulation tick, after the pile has moved. Returns whether any ball dropped.
        internal bool Feed(Action<BallState, Vector3> launch)
        {
            int released = 0;
            for (int i = 0; i < R && Remaining > 0; i++)
            {
                var cell = new Vector2Int(column * R + i, topRow);
                if (!grid.IsEmpty(cell)) continue;
                var ball = new BallState(queue[next], cell);
                grid.Add(ball);
                Vector3 from = sites.Count > 0 ? sites[0] : grid.CellToLocal(cell);
                if (visuals.Count > 0)
                {
                    ball.Visual = visuals[0];
                    if (ball.Visual != null) from = ball.Visual.localPosition;
                    visuals.RemoveAt(0);
                    starts.RemoveAt(0);
                    scales.RemoveAt(0);
                    entering.RemoveAt(0);
                }
                else ball.Visual = pool.Rent(ball.Color);
                next++;
                released++;
                launch(ball, from);
            }
            if (released == 0) return false;
            Restack(true);
            UpdateCounter();
            return true;
        }

        internal void Render(float amount)
        {
            if (!rolling) return;
            // Linear, so a belt that keeps flowing tick after tick rolls at an even speed.
            for (int i = 0; i < visuals.Count; i++)
            {
                if (visuals[i] == null) continue;
                visuals[i].localPosition = Vector3.LerpUnclamped(starts[i], sites[i], amount);
                if (entering[i]) visuals[i].localScale = scales[i] * amount;
            }
            if (amount >= 1f) rolling = false;
        }

        // Every ball moves up to the site its new place in line rests on, and the balls behind
        // them fill the track from the chute.
        private void Restack(bool animate)
        {
            for (int i = 0; i < visuals.Count; i++)
            {
                starts[i] = visuals[i] != null ? visuals[i].localPosition : sites[i];
                entering[i] = false;
            }
            while (visuals.Count < sites.Count && next + visuals.Count < queue.Count)
            {
                int index = visuals.Count;
                Transform visual = pool.Rent(queue[next + index]);
                Vector3 scale = visual != null ? visual.localScale : Vector3.one;
                if (visual != null)
                {
                    visual.localPosition = sites[index];
                    if (animate) visual.localScale = Vector3.zero;
                }
                visuals.Add(visual);
                starts.Add(sites[index]);
                scales.Add(scale);
                entering.Add(animate);
            }
            rolling = animate;
        }

        private void UpdateCounter()
        {
            if (counter == null) return;
            counter.gameObject.SetActive(Remaining > 0);
            counter.text = Remaining.ToString();
        }
    }
}
