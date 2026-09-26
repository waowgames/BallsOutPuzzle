using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace BallsOut
{
    // The conveyor over the reservoir. Its queue rides the belt (ConveyorTrack.Belt) four lanes abreast,
    // in rows, head row at the gate. Whenever a top-row site under the gate is free, the next ball in line
    // drops into it; once the head row is empty the whole belt glides one row forward, and a new row
    // slides in through the chute. A counter on the chute's cap shows everything still on the belt.
    public sealed class BallConveyorSystem
    {
        private const int R = LevelDefinition.MicroResolution;
        private const int Lanes = ConveyorTrack.Lanes;
        private readonly BallMicroGrid grid;
        private readonly BallPool pool;
        private readonly int column;
        private readonly int topRow;
        private readonly List<BallColorDefinition> queue = new List<BallColorDefinition>();
        private readonly ConveyorTrack.Belt belt;
        private readonly int rows;
        // visuals[i] shows queue[next + i]. Balls that have just come in through the chute grow to
        // their full scale while the belt moves.
        private readonly List<Transform> visuals = new List<Transform>();
        private readonly List<Vector3> scales = new List<Vector3>();
        private readonly List<bool> entering = new List<bool>();
        private readonly TextMeshPro counter;
        private int next;
        // Rows the belt is gliding forward during the current tick.
        private int advance;
        public int Remaining => queue.Count - next;
        private int HeadRow => next / Lanes;

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
            belt = new ConveyorTrack.Belt(level, visualHeight);
            rows = belt.Rows;
            float s = level.macroCellSize;
            counter = BallFeederSystem.CreateCounter(parent, s, "Conveyor Counter",
                new Vector3((ConveyorTrack.ChuteColumn(level) + 0.5f) * s, s * 0.36f, ConveyorTrack.ChuteTop(level) + s * 0.13f));
            Load(false);
            Render(1f);
            UpdateCounter();
        }

        // Called once per simulation tick, after the pile has moved. Returns whether any ball dropped.
        internal bool Feed(Action<BallState, Vector3> launch)
        {
            int released = 0, headRow = HeadRow;
            for (int i = 0; i < R && Remaining > 0; i++)
            {
                var cell = new Vector2Int(column * R + i, topRow);
                if (!grid.IsEmpty(cell)) continue;
                var ball = new BallState(queue[next], cell);
                grid.Add(ball);
                Vector3 from = Place(next, 1f);
                if (visuals.Count > 0)
                {
                    ball.Visual = visuals[0];
                    if (ball.Visual != null) from = ball.Visual.localPosition;
                    visuals.RemoveAt(0);
                    scales.RemoveAt(0);
                    entering.RemoveAt(0);
                }
                else ball.Visual = pool.Rent(ball.Color);
                next++;
                released++;
                launch(ball, from);
            }
            if (released == 0) return false;
            // A partly emptied head row waits for the rest of its balls; the belt moves by whole rows.
            advance = HeadRow - headRow;
            for (int i = 0; i < entering.Count; i++) entering[i] = false;
            if (advance > 0) Load(true);
            UpdateCounter();
            return true;
        }

        internal void Render(float amount)
        {
            if (advance == 0) return;
            // Linear, so a belt that keeps flowing tick after tick glides at an even speed.
            for (int i = 0; i < visuals.Count; i++)
            {
                if (visuals[i] == null) continue;
                visuals[i].localPosition = Place(next + i, amount);
                if (entering[i]) visuals[i].localScale = scales[i] * amount;
            }
            if (amount >= 1f) advance = 0;
        }

        // Where queue[index] rides, `amount` of the way through the current tick's glide.
        private Vector3 Place(int index, float amount)
        {
            int lane = index % Lanes;
            return belt.Point(lane, belt.Distance(lane, index / Lanes - HeadRow) + (1f - amount) * advance * belt.Spacing);
        }

        // Fills the rows that fit on the belt; with `entering` they slide in from beyond the chute's cap.
        private void Load(bool entering)
        {
            int end = Mathf.Min(queue.Count, (HeadRow + rows) * Lanes);
            while (next + visuals.Count < end)
            {
                Transform visual = pool.Rent(queue[next + visuals.Count]);
                Vector3 scale = visual != null ? visual.localScale : Vector3.one;
                if (visual != null && entering) visual.localScale = Vector3.zero;
                visuals.Add(visual);
                scales.Add(scale);
                this.entering.Add(entering);
            }
            if (!entering) advance = 1;
        }

        private void UpdateCounter()
        {
            if (counter == null) return;
            counter.gameObject.SetActive(Remaining > 0);
            counter.text = Remaining.ToString();
        }
    }
}
