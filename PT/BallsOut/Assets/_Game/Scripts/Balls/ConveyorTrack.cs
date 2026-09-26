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

        // Rest positions of the balls on the belt, head (at the gate) first. Each lane is walked on its
        // own, so the outer lane of a bend holds more balls than the inner one, as a loose pile would.
        internal static List<Vector3> Sites(LevelDefinition level, float height)
        {
            var points = new List<Vector2>();
            var normals = new List<Vector2>();
            Centerline(level, points, normals);
            var along = new float[points.Count];
            for (int i = 1; i < points.Count; i++) along[i] = along[i - 1] + (points[i] - points[i - 1]).magnitude;
            float spacing = Pitch * level.macroCellSize;
            float end = along[along.Length - 1] - spacing * 0.5f;
            var sites = new List<(float key, int lane, Vector2 position)>();
            for (int lane = 0; lane < Lanes; lane++)
            {
                float offset = (lane - (Lanes - 1) * 0.5f) * spacing;
                Vector2 previous = points[0] + normals[0] * offset;
                float walked = 0f, next = spacing * 0.5f;
                for (int i = 1; i < points.Count; i++)
                {
                    Vector2 point = points[i] + normals[i] * offset;
                    float segment = (point - previous).magnitude;
                    while (segment > 0f && walked + segment >= next)
                    {
                        float t = (next - walked) / segment;
                        float key = Mathf.Lerp(along[i - 1], along[i], t);
                        if (key > end) break;
                        sites.Add((key, lane, Vector2.Lerp(previous, point, t)));
                        next += spacing;
                    }
                    walked += segment;
                    previous = point;
                }
            }
            sites.Sort((a, b) => a.key != b.key ? a.key.CompareTo(b.key) : a.lane.CompareTo(b.lane));
            var result = new List<Vector3>(sites.Count);
            foreach (var site in sites) result.Add(new Vector3(site.position.x, height, site.position.y));
            return result;
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
