using System.Collections.Generic;
using UnityEngine;

namespace BallsOut
{
    // Sweeps a cross-section around the authored boundary, once per level load.
    internal static class BoardRimMesh
    {
        internal readonly struct Edge
        {
            internal readonly Vector3 From, To;
            internal Edge(Vector3 from, Vector3 to) { From = from; To = to; }
        }

        // X runs outward from the authored edge and Y up, both in macro cells.
        // Segment i (points i..i+1) uses submesh Materials[i]; points go inner side first, over the top.
        internal sealed class Style
        {
            internal readonly Vector2[] Profile;
            internal readonly Vector2[] Normals;
            internal readonly int[] Materials;
            // Corner radius at the innermost and outermost profile point. Convex corners grow outward
            // so the plate's outer rim is soft while its inner lip hugs the square cells.
            internal readonly Vector2 Convex, Concave;
            internal readonly float Inner, Outer;

            internal Style(List<Vector2> profile, List<int> materials, Vector2 convex, Vector2 concave)
            {
                Profile = profile.ToArray();
                Materials = materials.ToArray();
                Convex = convex;
                Concave = concave;
                Inner = float.PositiveInfinity;
                Outer = float.NegativeInfinity;
                foreach (Vector2 point in Profile)
                {
                    Inner = Mathf.Min(Inner, point.x);
                    Outer = Mathf.Max(Outer, point.x);
                }
                Normals = new Vector2[Profile.Length];
                for (int i = 0; i < Profile.Length; i++)
                {
                    Vector2 tangent = (Profile[Mathf.Min(i + 1, Profile.Length - 1)] - Profile[i]).normalized +
                        (Profile[i] - Profile[Mathf.Max(0, i - 1)]).normalized;
                    Normals[i] = new Vector2(-tangent.y, tangent.x).normalized;
                }
            }
        }

        private readonly struct Sample
        {
            // Point = Corner + Bisector * (e - Turn * r) + Outward * (Turn * r), r lerped across the profile.
            internal readonly Vector3 Corner, Bisector, Outward;
            internal readonly float Turn;
            internal readonly Vector2 Radius;

            internal Sample(Vector3 corner, Vector3 bisector, Vector3 outward, float turn, Vector2 radius)
            {
                Corner = corner; Bisector = bisector; Outward = outward; Turn = turn; Radius = radius;
            }
        }

        private const int ArcSteps = 8;
        private const float Height = 0.3f;

        // The board plate: a lit inner lip, a flat top and a rounded outer shoulder over a dark side.
        internal static readonly Style Frame = CreateFrame();
        // The plate strip between the reservoir and the grid, flush with the frame on its reservoir side.
        internal static readonly Style Bar = CreateBar();
        // Thin reservoir partition; it tucks under the frame and bar tops at both ends.
        internal static readonly Style Slat = CreateSlat();

        internal static void Append(List<Edge> edges, Style style, float scale, List<Vector3> vertices,
            List<Vector3> normals, List<int>[] triangles)
        {
            var starts = new Dictionary<Vector3Int, List<int>>();
            var ends = new HashSet<Vector3Int>();
            for (int i = 0; i < edges.Count; i++)
            {
                Vector3Int key = Key(edges[i].From, scale);
                if (!starts.TryGetValue(key, out var outgoing)) starts[key] = outgoing = new List<int>();
                outgoing.Add(i);
                ends.Add(Key(edges[i].To, scale));
            }
            var used = new bool[edges.Count];
            var path = new List<Vector3>();
            var samples = new List<Sample>();
            // Open boundaries first (e.g. the funnel mouth), then closed outlines and holes.
            for (int pass = 0; pass < 2; pass++)
                for (int start = 0; start < edges.Count; start++)
                {
                    if (used[start] || (pass == 0 && ends.Contains(Key(edges[start].From, scale)))) continue;
                    path.Clear();
                    int current = start;
                    path.Add(edges[current].From);
                    while (current >= 0)
                    {
                        used[current] = true;
                        Edge edge = edges[current];
                        path.Add(edge.To);
                        int next = -1;
                        float bestTurn = float.NegativeInfinity;
                        if (starts.TryGetValue(Key(edge.To, scale), out var outgoing))
                            foreach (int candidate in outgoing)
                            {
                                if (used[candidate]) continue;
                                Vector3 incoming = (edge.To - edge.From).normalized;
                                Vector3 direction = (edges[candidate].To - edges[candidate].From).normalized;
                                float turn = Vector3.SignedAngle(incoming, direction, Vector3.up);
                                if (turn > bestTurn) { bestTurn = turn; next = candidate; }
                            }
                        if (Key(edge.To, scale) == Key(path[0], scale)) break;
                        current = next;
                    }
                    bool closed = Key(path[0], scale) == Key(path[path.Count - 1], scale);
                    if (closed) path.RemoveAt(path.Count - 1);
                    Simplify(path, closed);
                    RoundPath(path, closed, style, scale, samples);
                    Sweep(samples, closed, style, scale, vertices, normals, triangles);
                }
        }

        private static Vector3Int Key(Vector3 p, float scale) =>
            new Vector3Int(Mathf.RoundToInt(p.x / scale * 10000f),
                Mathf.RoundToInt(p.y / scale * 10000f), Mathf.RoundToInt(p.z / scale * 10000f));

        private static Vector3 Outward(Vector3 direction) => new Vector3(-direction.z, 0f, direction.x);

        private static void Simplify(List<Vector3> path, bool closed)
        {
            for (int i = path.Count - 1; i >= 0 && path.Count > 2; i--)
            {
                if (!closed && (i == 0 || i == path.Count - 1)) continue;
                Vector3 before = (path[i] - path[(i + path.Count - 1) % path.Count]).normalized;
                Vector3 after = (path[(i + 1) % path.Count] - path[i]).normalized;
                if (Vector3.Dot(before, after) > 0.9999f) path.RemoveAt(i);
            }
        }

        // Signed turn in radians at a path corner (positive = clockwise from above = convex) and tan(|turn|/2).
        private static float Turn(List<Vector3> path, bool closed, int i, out float tangent)
        {
            tangent = 0f;
            if (!closed && (i == 0 || i == path.Count - 1)) return 0f;
            Vector3 incoming = (path[i] - path[(i + path.Count - 1) % path.Count]).normalized;
            Vector3 outgoing = (path[(i + 1) % path.Count] - path[i]).normalized;
            float angle = Vector3.SignedAngle(incoming, outgoing, Vector3.up) * Mathf.Deg2Rad;
            tangent = Mathf.Tan(Mathf.Abs(angle) * 0.5f);
            return angle;
        }

        private static void RoundPath(List<Vector3> path, bool closed, Style style, float scale, List<Sample> samples)
        {
            samples.Clear();
            int count = path.Count;
            var turns = new float[count];
            var tangents = new float[count];
            for (int i = 0; i < count; i++) turns[i] = Turn(path, closed, i, out tangents[i]);
            for (int i = 0; i < count; i++)
            {
                Vector3 p = path[i];
                int previous = (i + count - 1) % count, next = (i + 1) % count;
                if (!closed && (i == 0 || i == count - 1))
                {
                    Vector3 along = i == 0 ? path[next] - p : p - path[previous];
                    Vector3 end = Outward(along.normalized);
                    samples.Add(new Sample(p, end, end, 0f, Vector2.zero));
                    continue;
                }
                Vector3 incoming = (p - path[previous]).normalized;
                Vector3 outgoing = (path[next] - p).normalized;
                if (tangents[i] < 0.001f)
                {
                    samples.Add(new Sample(p, Outward(outgoing), Outward(outgoing), 0f, Vector2.zero));
                    continue;
                }
                float angle = turns[i];
                float turn = Mathf.Sign(angle);
                Vector3 before = Outward(incoming), after = Outward(outgoing);
                Vector3 bisector = (before + after) / Mathf.Max(0.1f, 1f + Vector3.Dot(before, after));
                Vector2 radius = (turn > 0f ? style.Convex : style.Concave) * scale;
                if (turn > 0f)
                {
                    // Past this growth rate the outer rim would fold back over the inner lip on sharp corners.
                    float cosine = Mathf.Cos(Mathf.Abs(angle) * 0.5f);
                    float growth = 0.8f / Mathf.Max(0.001f, 1f - cosine) * (style.Outer - style.Inner) * scale;
                    radius.y = Mathf.Min(radius.y, radius.x + growth);
                }
                radius.x = Mathf.Clamp(radius.x, 0.001f * scale, Limit(path, turns, tangents, previous, i, next, style.Inner * scale));
                radius.y = Mathf.Clamp(radius.y, 0.001f * scale, Limit(path, turns, tangents, previous, i, next, style.Outer * scale));
                int steps = Mathf.Max(2, Mathf.CeilToInt(Mathf.Abs(angle) / (Mathf.PI * 0.5f) * ArcSteps));
                for (int step = 0; step <= steps; step++)
                {
                    Quaternion rotation = Quaternion.AngleAxis(angle * Mathf.Rad2Deg * step / steps, Vector3.up);
                    samples.Add(new Sample(p, bisector, rotation * before, turn, radius));
                }
            }
        }

        // Largest radius at edge distance e that leaves each neighbouring side, measured on its own
        // offset line (longer past convex corners, shorter past concave ones), half for the next arc.
        private static float Limit(List<Vector3> path, float[] turns, float[] tangents, int previous, int i, int next, float e)
        {
            float own = Mathf.Sign(turns[i]) * tangents[i];
            float before = (path[i] - path[previous]).magnitude + e * (Mathf.Sign(turns[previous]) * tangents[previous] + own);
            float after = (path[next] - path[i]).magnitude + e * (own + Mathf.Sign(turns[next]) * tangents[next]);
            return Mathf.Max(0.001f, 0.48f * Mathf.Min(before, after) / tangents[i]);
        }

        private static Vector3 Point(Sample sample, Style style, Vector2 profile, float scale)
        {
            float e = profile.x * scale;
            float t = Mathf.InverseLerp(style.Inner, style.Outer, profile.x);
            float r = Mathf.Lerp(sample.Radius.x, sample.Radius.y, t) * sample.Turn;
            return sample.Corner + sample.Bisector * (e - r) + sample.Outward * r + Vector3.up * (profile.y * scale);
        }

        private static void Sweep(List<Sample> samples, bool closed, Style style, float scale, List<Vector3> vertices,
            List<Vector3> normals, List<int>[] triangles)
        {
            int first = vertices.Count;
            int count = style.Profile.Length;
            foreach (Sample sample in samples)
                for (int p = 0; p < count; p++)
                {
                    vertices.Add(Point(sample, style, style.Profile[p], scale));
                    normals.Add(sample.Outward * style.Normals[p].x + Vector3.up * style.Normals[p].y);
                }
            for (int i = 0; i < samples.Count - (closed ? 0 : 1); i++)
                for (int p = 0; p < count - 1; p++)
                {
                    int a = first + i * count + p;
                    int b = first + ((i + 1) % samples.Count) * count + p;
                    var indices = triangles[style.Materials[p]];
                    indices.Add(a); indices.Add(a + 1); indices.Add(b + 1);
                    indices.Add(a); indices.Add(b + 1); indices.Add(b);
                }
            if (closed || samples.Count < 2) return;
            int capMaterial = style.Materials[count / 2];
            Cap(first, Outward(samples[0].Outward), count, vertices, normals, triangles[capMaterial]);
            Cap(first + (samples.Count - 1) * count, -Outward(samples[samples.Count - 1].Outward), count,
                vertices, normals, triangles[capMaterial]);
        }

        private static void Cap(int ring, Vector3 normal, int count, List<Vector3> vertices, List<Vector3> normals,
            List<int> triangles)
        {
            int start = vertices.Count;
            for (int p = 0; p < count; p++)
            {
                vertices.Add(vertices[ring + p]);
                normals.Add(normal);
            }
            for (int p = 1; p < count - 1; p++)
            {
                bool flip = Vector3.Dot(Vector3.Cross(vertices[start + p] - vertices[start],
                    vertices[start + p + 1] - vertices[start]), normal) < 0f;
                triangles.Add(start);
                triangles.Add(start + (flip ? p + 1 : p));
                triangles.Add(start + (flip ? p : p + 1));
            }
        }

        // Submeshes: 1 dark side, 2 top, 3 lit lip.
        private static Style CreateFrame()
        {
            var profile = new List<Vector2>();
            var materials = new List<int>();
            // The foot reaches under the floors so a concave corner never opens a gap at the seam.
            profile.Add(new Vector2(-0.02f, 0f));
            Line(profile, materials, new Vector2(0f, 0.02f), 3);
            Line(profile, materials, new Vector2(0.025f, Height - 0.06f), 3);
            Arc(profile, materials, new Vector2(0.085f, Height - 0.06f), 0.06f, 180f, 90f, 4, 3);
            Line(profile, materials, new Vector2(0.2f, Height), 2);
            Arc(profile, materials, new Vector2(0.2f, Height - 0.1f), 0.1f, 90f, 0f, 5, 2);
            // A slab edge below the floor gives the plate visible thickness along the front.
            Line(profile, materials, new Vector2(0.3f, -0.08f), 1);
            return new Style(profile, materials, new Vector2(0.1f, 0.44f), new Vector2(0.03f, 0.2f));
        }

        private static Style CreateBar()
        {
            var profile = new List<Vector2>();
            var materials = new List<int>();
            // The grid-side lip faces the camera; the reservoir side drops under the depot floor.
            profile.Add(new Vector2(0f, 0.02f));
            Line(profile, materials, new Vector2(0.025f, Height - 0.06f), 3);
            Arc(profile, materials, new Vector2(0.085f, Height - 0.06f), 0.06f, 180f, 90f, 4, 3);
            Line(profile, materials, new Vector2(0.215f, Height), 2);
            Arc(profile, materials, new Vector2(0.215f, Height - 0.06f), 0.06f, 90f, 0f, 4, 2);
            // Any wider and the top would hide the front row of balls from the tilted camera.
            Line(profile, materials, new Vector2(0.3f, 0.1f), 2);
            return new Style(profile, materials, new Vector2(0.06f, 0.06f), new Vector2(0.06f, 0.06f));
        }

        private static Style CreateSlat()
        {
            var profile = new List<Vector2>();
            var materials = new List<int>();
            const float half = 0.035f;
            profile.Add(new Vector2(-half, 0.1f));
            Line(profile, materials, new Vector2(-half, Height - 0.01f - half), 2);
            Arc(profile, materials, new Vector2(0f, Height - 0.01f - half), half, 180f, 0f, 6, 3);
            Line(profile, materials, new Vector2(half, 0.1f), 2);
            return new Style(profile, materials, new Vector2(0.03f, 0.03f), new Vector2(0.03f, 0.03f));
        }

        private static void Line(List<Vector2> profile, List<int> materials, Vector2 to, int material)
        {
            profile.Add(to);
            materials.Add(material);
        }

        private static void Arc(List<Vector2> profile, List<int> materials, Vector2 center, float radius,
            float from, float to, int steps, int material)
        {
            for (int i = 1; i <= steps; i++)
            {
                float angle = Mathf.Lerp(from, to, (float)i / steps) * Mathf.Deg2Rad;
                Line(profile, materials, center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius, material);
            }
        }
    }
}
