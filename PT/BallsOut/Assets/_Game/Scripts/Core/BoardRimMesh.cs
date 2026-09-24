using System.Collections.Generic;
using UnityEngine;

namespace BallsOut
{
    // Sweeps one rounded profile around the authored boundary, once per level load.
    internal static class BoardRimMesh
    {
        internal readonly struct Edge
        {
            internal readonly Vector3 From, To;
            internal Edge(Vector3 from, Vector3 to) { From = from; To = to; }
        }

        private readonly struct Sample
        {
            internal readonly Vector3 Position, Outward;
            internal Sample(Vector3 position, Vector3 outward) { Position = position; Outward = outward; }
        }

        private const float Offset = 0.19f;
        private const float CornerRadius = 0.28f;
        private const int ArcSteps = 8;
        private static readonly Vector2[] Profile = CreateProfile();
        private static readonly Vector2[] ProfileNormals = CreateProfileNormals();

        internal static void Append(List<Edge> edges, float scale, List<Vector3> vertices,
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
                    RoundPath(path, closed, scale, samples);
                    Sweep(samples, closed, scale, vertices, normals, triangles);
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

        private static void RoundPath(List<Vector3> path, bool closed, float scale, List<Sample> samples)
        {
            samples.Clear();
            var centers = new Vector3[path.Count];
            for (int i = 0; i < path.Count; i++)
            {
                Vector3 before = (path[i] - path[(i + path.Count - 1) % path.Count]).normalized;
                Vector3 after = (path[(i + 1) % path.Count] - path[i]).normalized;
                if (!closed && i == 0) before = after;
                if (!closed && i == path.Count - 1) after = before;
                Vector3 bisector = Outward(before) + Outward(after);
                centers[i] = path[i] + bisector * (Offset * scale / Mathf.Max(0.1f, 1f + Vector3.Dot(before, after)));
            }
            for (int i = 0; i < centers.Length; i++)
            {
                Vector3 p = centers[i];
                Vector3 incoming = p - centers[(i + centers.Length - 1) % centers.Length];
                Vector3 outgoing = centers[(i + 1) % centers.Length] - p;
                if (!closed && (i == 0 || i == centers.Length - 1))
                {
                    samples.Add(new Sample(p, Outward((i == 0 ? outgoing : incoming).normalized)));
                    continue;
                }
                float available = Mathf.Min(incoming.magnitude, outgoing.magnitude) * 0.45f;
                incoming.Normalize(); outgoing.Normalize();
                float angle = Vector3.SignedAngle(incoming, outgoing, Vector3.up) * Mathf.Deg2Rad;
                float tangent = Mathf.Tan(Mathf.Abs(angle) * 0.5f);
                if (tangent < 0.001f)
                {
                    samples.Add(new Sample(p, Outward(outgoing)));
                    continue;
                }
                float trim = Mathf.Min(CornerRadius * scale * tangent, available);
                float radius = trim / tangent;
                float turn = Mathf.Sign(angle);
                Vector3 first = p - incoming * trim;
                Vector3 center = first - Outward(incoming) * (radius * turn);
                int steps = Mathf.Max(2, Mathf.CeilToInt(Mathf.Abs(angle) / (Mathf.PI * 0.5f) * ArcSteps));
                for (int step = 0; step <= steps; step++)
                {
                    Quaternion rotation = Quaternion.AngleAxis(angle * Mathf.Rad2Deg * step / steps, Vector3.up);
                    samples.Add(new Sample(center + rotation * (first - center), rotation * Outward(incoming)));
                }
            }
        }

        private static Vector2[] CreateProfile()
        {
            var profile = new Vector2[ArcSteps + 6];
            // A small foot overlaps the floor; the raised lip stays outside playable cells.
            profile[0] = new Vector2(-0.20f, 0.02f);
            profile[1] = new Vector2(-0.14f, 0.09f);
            for (int i = 0; i <= ArcSteps; i++)
            {
                float angle = Mathf.PI - Mathf.PI * i / ArcSteps;
                profile[i + 2] = new Vector2(Mathf.Cos(angle) * 0.14f, 0.22f + Mathf.Sin(angle) * 0.14f);
            }
            profile[ArcSteps + 3] = new Vector2(0.14f, 0.09f);
            profile[ArcSteps + 4] = new Vector2(0.12f, 0.035f);
            profile[ArcSteps + 5] = new Vector2(0.09f, 0.02f);
            return profile;
        }

        private static Vector2[] CreateProfileNormals()
        {
            var normals = new Vector2[Profile.Length];
            for (int i = 0; i < Profile.Length; i++)
            {
                Vector2 tangent = (Profile[Mathf.Min(i + 1, Profile.Length - 1)] - Profile[i]).normalized +
                    (Profile[i] - Profile[Mathf.Max(0, i - 1)]).normalized;
                normals[i] = new Vector2(-tangent.y, tangent.x).normalized;
            }
            return normals;
        }

        private static void Sweep(List<Sample> samples, bool closed, float scale, List<Vector3> vertices,
            List<Vector3> normals, List<int>[] triangles)
        {
            int first = vertices.Count;
            int count = Profile.Length;
            foreach (Sample sample in samples)
                for (int p = 0; p < count; p++)
                {
                    vertices.Add(sample.Position + (sample.Outward * Profile[p].x + Vector3.up * Profile[p].y) * scale);
                    normals.Add(sample.Outward * ProfileNormals[p].x + Vector3.up * ProfileNormals[p].y);
                }
            for (int i = 0; i < samples.Count - (closed ? 0 : 1); i++)
                for (int p = 0; p < count - 1; p++)
                {
                    int a = first + i * count + p;
                    int b = first + ((i + 1) % samples.Count) * count + p;
                    int material = p >= ArcSteps + 3 ? 1 : p == 5 || p == 6 ? 3 : 2;
                    var indices = triangles[material];
                    indices.Add(a); indices.Add(a + 1); indices.Add(b + 1);
                    indices.Add(a); indices.Add(b + 1); indices.Add(b);
                }
            if (closed || samples.Count < 2) return;
            Cap(first, Outward(samples[0].Outward), vertices, normals, triangles[2]);
            Cap(first + (samples.Count - 1) * count, -Outward(samples[samples.Count - 1].Outward), vertices, normals, triangles[2]);
        }

        private static void Cap(int ring, Vector3 normal, List<Vector3> vertices, List<Vector3> normals, List<int> triangles)
        {
            int start = vertices.Count;
            for (int p = 0; p < Profile.Length; p++)
            {
                vertices.Add(vertices[ring + p]);
                normals.Add(normal);
            }
            for (int p = 1; p < Profile.Length - 1; p++)
            {
                bool flip = Vector3.Dot(Vector3.Cross(vertices[start + p] - vertices[start],
                    vertices[start + p + 1] - vertices[start]), normal) < 0f;
                triangles.Add(start);
                triangles.Add(start + (flip ? p + 1 : p));
                triangles.Add(start + (flip ? p : p + 1));
            }
        }
    }
}
