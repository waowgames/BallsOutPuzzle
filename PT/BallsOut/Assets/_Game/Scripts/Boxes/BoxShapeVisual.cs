using System.Collections.Generic;
using UnityEngine;

namespace BallsOut
{
    // Matches the eight rounded rings of the authored Box_* meshes. Built once per shape.
    internal static class BoxShapeVisual
    {
        private static readonly Dictionary<BoxShapeDefinition, Mesh> Meshes = new Dictionary<BoxShapeDefinition, Mesh>();
        // X is the inset from the cell boundary; Y is the unscaled mesh height.
        private static readonly Vector2[] Profile =
        {
            new Vector2(0.049f, 0f), new Vector2(0.025f, 0.024f),
            new Vector2(0.025f, 0.266f), new Vector2(0.049f, 0.29f),
            new Vector2(0.081f, 0.29f), new Vector2(0.105f, 0.266f),
            new Vector2(0.105f, 0.084f), new Vector2(0.129f, 0.06f)
        };

        internal static GameObject Create(BoxShapeDefinition shape, Material material, Transform parent,
            float cellSize, PrefabRegistry registry, out Vector3 fillOffset)
        {
            if (!Meshes.TryGetValue(shape, out Mesh mesh))
            {
                mesh = BuildMesh(shape);
                Meshes.Add(shape, mesh);
            }
            float elevation = 0.22f;
            float depthScale = 2.2275f;
            fillOffset = new Vector3(0f, elevation, 0f);
            MeshRenderer referenceRenderer = null;
            if (registry != null)
                foreach (BoxVisualEntry entry in registry.boxes)
                {
                    if (entry.prefab == null) continue;
                    elevation = entry.localOffset.y;
                    depthScale = entry.localScale == Vector3.zero ? 1f : Mathf.Abs(entry.localScale.y);
                    fillOffset = new Vector3(0f, entry.fillOffset.y, 0f);
                    referenceRenderer = entry.prefab.GetComponentInChildren<MeshRenderer>();
                    break;
                }
            var visual = new GameObject("Box Shape", typeof(MeshFilter), typeof(MeshRenderer));
            visual.transform.SetParent(parent, false);
            // Registry elevation/depth are absolute, just as for the prefab branch in BoxController.
            visual.transform.localPosition = Vector3.up * elevation;
            visual.transform.localScale = new Vector3(cellSize, depthScale, cellSize);
            visual.GetComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = visual.GetComponent<MeshRenderer>();
            if (referenceRenderer != null)
            {
                renderer.shadowCastingMode = referenceRenderer.shadowCastingMode;
                renderer.receiveShadows = referenceRenderer.receiveShadows;
                renderer.lightProbeUsage = referenceRenderer.lightProbeUsage;
                renderer.reflectionProbeUsage = referenceRenderer.reflectionProbeUsage;
            }
            renderer.sharedMaterial = material;
            PrefabRegistry.ApplyMaterial(visual, material);
            return visual;
        }

        private static Mesh BuildMesh(BoxShapeDefinition shape)
        {
            var vertices = new List<Vector3>();
            var uv = new List<Vector2>();
            var triangles = new List<int>();
            foreach (List<Vector2> outline in Outlines(shape))
            {
                int first = vertices.Count;
                foreach (Vector2 profile in Profile) AppendRing(outline, profile, vertices, uv);
                int count = (vertices.Count - first) / Profile.Length;
                for (int ring = 0; ring < Profile.Length - 1; ring++)
                    for (int i = 0; i < count; i++)
                    {
                        int a = first + ring * count + i;
                        int b = first + ring * count + (i + 1) % count;
                        triangles.Add(a); triangles.Add(a + count); triangles.Add(b);
                        triangles.Add(b); triangles.Add(a + count); triangles.Add(b + count);
                    }
                Cap(first, count, false, vertices, triangles);
                Cap(first + (Profile.Length - 1) * count, count, true, vertices, triangles);
            }
            var mesh = new Mesh { name = "Box " + shape.name };
            if (vertices.Count > ushort.MaxValue) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uv);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static List<List<Vector2>> Outlines(BoxShapeDefinition shape)
        {
            var cells = new HashSet<Vector2Int>(shape.Cells);
            var edges = new Dictionary<Vector2Int, List<Vector2Int>>();
            foreach (Vector2Int cell in cells)
            {
                Vector2Int p = cell * 2 - Vector2Int.one;
                if (!cells.Contains(cell + Vector2Int.down)) Add(p, p + Vector2Int.right * 2);
                if (!cells.Contains(cell + Vector2Int.right)) Add(p + Vector2Int.right * 2, p + Vector2Int.one * 2);
                if (!cells.Contains(cell + Vector2Int.up)) Add(p + Vector2Int.one * 2, p + Vector2Int.up * 2);
                if (!cells.Contains(cell + Vector2Int.left)) Add(p + Vector2Int.up * 2, p);
            }
            var outlines = new List<List<Vector2>>();
            while (edges.Count > 0)
            {
                // Stable order also matches the triangulation/shading of the original mesh assets.
                Vector2Int start = new Vector2Int(int.MaxValue, int.MaxValue);
                foreach (Vector2Int point in edges.Keys)
                    if (point.y < start.y || point.y == start.y && point.x < start.x) start = point;
                var outline = new List<Vector2>();
                Vector2Int current = start;
                Vector2Int incoming = Vector2Int.down;
                do
                {
                    outline.Add((Vector2)current * 0.5f);
                    List<Vector2Int> outgoing = edges[current];
                    int next = 0;
                    for (int i = 1; i < outgoing.Count; i++)
                        if (Cross(incoming, outgoing[i] - current) > Cross(incoming, outgoing[next] - current)) next = i;
                    Vector2Int end = outgoing[next];
                    outgoing.RemoveAt(next);
                    if (outgoing.Count == 0) edges.Remove(current);
                    incoming = end - current;
                    current = end;
                } while (current != start);
                for (int i = outline.Count - 1; i >= 0; i--)
                    if (Mathf.Abs(Cross(outline[i] - outline[(i + outline.Count - 1) % outline.Count],
                        outline[(i + 1) % outline.Count] - outline[i])) < 0.001f) outline.RemoveAt(i);
                outlines.Add(outline);
            }
            return outlines;

            void Add(Vector2Int from, Vector2Int to)
            {
                if (!edges.TryGetValue(from, out var outgoing)) edges.Add(from, outgoing = new List<Vector2Int>(1));
                outgoing.Add(to);
            }
        }

        private static void AppendRing(List<Vector2> outline, Vector2 profile, List<Vector3> vertices, List<Vector2> uv)
        {
            var inset = new Vector2[outline.Count];
            for (int i = 0; i < outline.Count; i++)
            {
                Vector2 before = (outline[i] - outline[(i + outline.Count - 1) % outline.Count]).normalized;
                Vector2 after = (outline[(i + 1) % outline.Count] - outline[i]).normalized;
                inset[i] = outline[i] + new Vector2(-before.y - after.y, before.x + after.x) * profile.x;
            }
            for (int i = 0; i < inset.Length; i++)
            {
                Vector2 corner = inset[i];
                Vector2 before = corner - inset[(i + inset.Length - 1) % inset.Length];
                Vector2 after = inset[(i + 1) % inset.Length] - corner;
                float radius = Mathf.Min(0.075f, Mathf.Min(before.magnitude, after.magnitude) * 0.45f);
                Vector2 from = corner - before.normalized * radius;
                Vector2 to = corner + after.normalized * radius;
                // Authored corners use four-segment quadratic curves, including concave turns.
                for (int step = 0; step <= 4; step++)
                {
                    float t = step * 0.25f;
                    Vector2 point = (1f - t) * (1f - t) * from + 2f * (1f - t) * t * corner + t * t * to;
                    vertices.Add(new Vector3(point.x, profile.y, point.y));
                    uv.Add(point);
                }
            }
        }

        private static void Cap(int start, int count, bool upward, List<Vector3> vertices, List<int> triangles)
        {
            var remaining = new List<int>(count);
            for (int i = 0; i < count; i++) remaining.Add(start + i);
            while (remaining.Count > 2)
            {
                bool clipped = false;
                for (int i = 0; i < remaining.Count; i++)
                {
                    int a = remaining[(i + remaining.Count - 1) % remaining.Count];
                    int b = remaining[i];
                    int c = remaining[(i + 1) % remaining.Count];
                    Vector2 p = XZ(vertices[a]), q = XZ(vertices[b]), r = XZ(vertices[c]);
                    if (Cross(q - p, r - q) <= 0.000001f) continue;
                    bool occupied = false;
                    foreach (int index in remaining)
                    {
                        if (index == a || index == b || index == c) continue;
                        Vector2 point = XZ(vertices[index]);
                        if (Cross(q - p, point - p) >= -0.000001f &&
                            Cross(r - q, point - q) >= -0.000001f &&
                            Cross(p - r, point - r) >= -0.000001f) { occupied = true; break; }
                    }
                    if (occupied) continue;
                    triangles.Add(a); triangles.Add(upward ? c : b); triangles.Add(upward ? b : c);
                    remaining.RemoveAt(i);
                    clipped = true;
                    break;
                }
                if (!clipped) break;
            }
        }

        private static Vector2 XZ(Vector3 point) => new Vector2(point.x, point.z);
        private static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;
    }
}
