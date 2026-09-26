using System.Collections.Generic;
using UnityEngine;

namespace BallsOut
{
    // Geometry of the conveyor over the reservoir, in board-local space. One-cell-wide runs are
    // stacked above the reservoir's top edge and joined by U-turns at alternate walls. The lowest run
    // bends down into a one-column gate over the reservoir; the highest one starts at a short chute
    // through the top frame, where balls come in. Thin slats wall the runs off from each other.
    internal static class ConveyorTrack
    {
        // Four lanes of balls ride side by side, which is also the gate's width in reservoir columns.
        internal const int Lanes = 4;
        // Lane and row spacing in macro cells: four lanes fit a one-cell run between its slats.
        internal const float Pitch = 0.23f;
        internal const float ChuteLength = 0.8f;
        // The slat over the reservoir sits just clear of its top row of balls.
        private const float SlatGap = 0.04f;
        private const float Step = 0.02f;

        internal static int Runs(LevelDefinition level) => Mathf.Clamp(level.conveyor.runs, 1, 4);
        // Centre line of the slat between the reservoir and the lowest run.
        internal static float Base(LevelDefinition level) => level.DepotTop + SlatGap * level.macroCellSize;
        internal static float Top(LevelDefinition level) => Base(level) + Runs(level) * level.macroCellSize;
        internal static float ChuteTop(LevelDefinition level) => Top(level) + ChuteLength * level.macroCellSize;

        // Traced back from the gate, the lowest run heads for the farther wall, so it is as long as possible.
        internal static bool FirstLeft(LevelDefinition level) => level.conveyor.column + 0.5f >= level.macroGridWidth * 0.5f;
        // Run k heads left when k and the first run agree; the chute sits where the top run ends.
        internal static bool HeadsLeft(LevelDefinition level, int run) => FirstLeft(level) == (run % 2 == 0);
        internal static bool ChuteLeft(LevelDefinition level) => HeadsLeft(level, Runs(level) - 1);
        internal static int ChuteColumn(LevelDefinition level) => ChuteLeft(level) ? 0 : level.macroGridWidth - 1;

        // Side-by-side lanes packed like a pile: lanes sit closer than the pitch, and every other
        // lane runs half a pitch behind, so neighbours nest diagonally.
        internal const float LaneSpacing = Pitch * 0.87f;

        // The belt's lanes as paths from the gate to the top of the chute. A ball keeps its lane and
        // glides along it, so the belt only ever moves forward; through a bend the outer lane is longer,
        // so a row fans out there instead of squeezing its inner balls together.
        internal sealed class Belt
        {
            private readonly Vector2[][] points;
            private readonly float[][] along;
            private readonly float height;
            internal float Spacing { get; }

            internal Belt(LevelDefinition level, float height)
            {
                this.height = height;
                Spacing = Pitch * level.macroCellSize;
                var center = new List<Vector2>();
                var normals = new List<Vector2>();
                Centerline(level, center, normals);
                points = new Vector2[Lanes][];
                along = new float[Lanes][];
                for (int lane = 0; lane < Lanes; lane++)
                {
                    float offset = (lane - (Lanes - 1) * 0.5f) * LaneSpacing * level.macroCellSize;
                    points[lane] = new Vector2[center.Count];
                    along[lane] = new float[center.Count];
                    for (int i = 0; i < center.Count; i++)
                    {
                        points[lane][i] = center[i] + normals[i] * offset;
                        if (i > 0) along[lane][i] = along[lane][i - 1] + (points[lane][i] - points[lane][i - 1]).magnitude;
                    }
                }
            }

            // Distance along its lane of the ball `row` rows back from the gate.
            internal float Distance(int lane, int row) => (row + 0.5f + (lane & 1) * 0.5f) * Spacing;

            // Rows that fit on every lane before the chute's cap.
            internal int Rows
            {
                get
                {
                    int rows = int.MaxValue;
                    for (int lane = 0; lane < Lanes; lane++)
                    {
                        float length = along[lane][along[lane].Length - 1] - Spacing * 0.5f;
                        int fit = 0;
                        while (Distance(lane, fit) <= length) fit++;
                        rows = Mathf.Min(rows, fit);
                    }
                    return rows;
                }
            }

            // Point on a lane; past the chute's end it carries straight on, where new balls come in from.
            internal Vector3 Point(int lane, float distance)
            {
                Vector2[] path = points[lane];
                float[] length = along[lane];
                int last = path.Length - 1;
                Vector2 point;
                if (distance >= length[last])
                    point = path[last] + (path[last] - path[last - 1]).normalized * (distance - length[last]);
                else
                {
                    int low = 0, high = last;
                    while (high - low > 1)
                    {
                        int mid = (low + high) / 2;
                        if (length[mid] <= distance) low = mid;
                        else high = mid;
                    }
                    float span = length[high] - length[low];
                    point = Vector2.Lerp(path[low], path[high], span > 0f ? (Mathf.Max(0f, distance) - length[low]) / span : 0f);
                }
                return new Vector3(point.x, height, point.y);
            }
        }

        // The belt's centre line from the gate back to the top of the chute, with left normals.
        private static void Centerline(LevelDefinition level, List<Vector2> points, List<Vector2> normals)
        {
            float s = level.macroCellSize;
            int width = level.macroGridWidth, column = level.conveyor.column, runs = Runs(level);
            float radius = s * 0.5f;
            var path = new Turtle(points, normals, new Vector2((column + 0.5f) * s, Base(level)), 90f);
            bool left = FirstLeft(level);
            path.Turn(radius, left ? 90f : -90f);
            float x = (left ? column : column + 1) * s;
            for (int run = 0; run < runs; run++)
            {
                bool headsLeft = HeadsLeft(level, run);
                float turn = headsLeft ? s : (width - 1) * s;
                path.Forward(Mathf.Abs(turn - x));
                x = turn;
                // A U-turn climbs one run and comes back over the same spot; the top run bends up into the chute.
                if (run < runs - 1) path.Turn(radius, headsLeft ? -180f : 180f);
                else path.Turn(radius, headsLeft ? -90f : 90f);
            }
            path.Forward(ChuteLength * s);
        }

        private sealed class Turtle
        {
            private readonly List<Vector2> points, normals;
            private Vector2 position;
            // Degrees counter-clockwise from +x, seen from above (+z is up the screen).
            private float heading;

            internal Turtle(List<Vector2> points, List<Vector2> normals, Vector2 start, float heading)
            {
                this.points = points;
                this.normals = normals;
                position = start;
                this.heading = heading;
                Add();
            }

            private Vector2 Direction => new Vector2(Mathf.Cos(heading * Mathf.Deg2Rad), Mathf.Sin(heading * Mathf.Deg2Rad));
            private Vector2 Left => new Vector2(-Direction.y, Direction.x);

            private void Add()
            {
                points.Add(position);
                normals.Add(Left);
            }

            internal void Forward(float length)
            {
                if (length <= 0f) return;
                int steps = Mathf.CeilToInt(length / Step);
                Vector2 from = position, direction = Direction;
                for (int i = 1; i <= steps; i++)
                {
                    position = from + direction * (length * i / steps);
                    Add();
                }
            }

            // Positive angles turn left.
            internal void Turn(float radius, float angle)
            {
                Vector2 center = position + Left * (radius * Mathf.Sign(angle));
                Vector2 arm = position - center;
                float start = heading;
                int steps = Mathf.Max(4, Mathf.CeilToInt(Mathf.Abs(angle) * Mathf.Deg2Rad * radius / Step));
                for (int i = 1; i <= steps; i++)
                {
                    float delta = angle * i / steps;
                    position = center + (Vector2)(Quaternion.Euler(0f, 0f, delta) * arm);
                    heading = start + delta;
                    Add();
                }
            }
        }
    }
}
