using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace BallsOut
{
    // Double-headed arrow on an axis-locked box: white on a dark outline, flat on the rim.
    // World-space decoration: no collider or per-frame updates.
    internal static class BoxAxisArrow
    {
        // Proportions in cells.
        private const float EndMargin = 0.17f;
        private const float MinLength = 0.56f;
        private const float ShaftHalfWidth = 0.034f;
        private const float HeadLength = 0.15f;
        private const float HeadHalfWidth = 0.115f;
        private const float Outline = 0.032f;
        private static readonly Color Fill = new Color(0.97f, 0.97f, 1f);
        private static readonly Color Edge = new Color(0.13f, 0.12f, 0.25f);
        private static readonly Dictionary<int, Mesh> Meshes = new Dictionary<int, Mesh>();
        private static Material fillMaterial;
        private static Material edgeMaterial;

        internal static GameObject Create(BoxController box, GameObject visual, float cellSize)
        {
            bool horizontal = box.MoveAxis == BoxMoveAxis.Horizontal;
            FindLongestRun(box, horizontal, out Vector2 center, out int run);
            float length = Mathf.Max(MinLength, run - EndMargin * 2f);

            float top = cellSize * 0.9f;
            foreach (Renderer renderer in visual.GetComponentsInChildren<Renderer>(true))
                top = Mathf.Max(top, box.transform.InverseTransformPoint(renderer.bounds.max).y);

            // Keep clear of the fill tab in the top-left corner.
            center += horizontal ? new Vector2(0f, -0.05f) : new Vector2(0.06f, -0.05f);
            var root = new GameObject("Move Axis Arrow");
            root.transform.SetParent(box.transform, false);
            root.transform.localPosition = new Vector3(center.x * cellSize, top + cellSize * 0.02f, center.y * cellSize);
            root.transform.localRotation = Quaternion.Euler(0f, horizontal ? 0f : 90f, 0f);
            root.transform.localScale = Vector3.one * cellSize;
            AddLayer(root.transform, ArrowMesh(length, Outline), GetMaterial(ref edgeMaterial, Edge, "Box Axis Arrow Edge"), 0f);
            AddLayer(root.transform, ArrowMesh(length, 0f), GetMaterial(ref fillMaterial, Fill, "Box Axis Arrow"), 0.004f);
            return root;
        }

        // The arrow runs along the longest straight stretch of the shape on its axis,
        // preferring the stretch nearest the footprint centre.
        private static void FindLongestRun(BoxController box, bool horizontal, out Vector2 center, out int run)
        {
            var cells = new HashSet<Vector2Int>(box.Shape.Cells);
            Vector2 centroid = Vector2.zero;
            foreach (Vector2Int cell in box.Shape.Cells) centroid += cell;
            centroid /= box.Shape.Cells.Count;
            Vector2Int step = horizontal ? Vector2Int.right : Vector2Int.up;
            center = centroid;
            run = 0;
            float best = float.PositiveInfinity;
            foreach (Vector2Int cell in box.Shape.Cells)
            {
                if (cells.Contains(cell - step)) continue;
                int length = 1;
                while (cells.Contains(cell + step * length)) length++;
                Vector2 middle = cell + (Vector2)step * ((length - 1) * 0.5f);
                float distance = (middle - centroid).sqrMagnitude;
                if (length < run || length == run && distance >= best) continue;
                run = length;
                best = distance;
                center = middle;
            }
        }

        private static void AddLayer(Transform parent, Mesh mesh, Material material, float height)
        {
            var layer = new GameObject(material != null ? material.name : "Arrow", typeof(MeshFilter), typeof(MeshRenderer));
            layer.transform.SetParent(parent, false);
            layer.transform.localPosition = Vector3.up * height;
            layer.GetComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer renderer = layer.GetComponent<MeshRenderer>();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            if (material != null) renderer.sharedMaterial = material;
        }

        // Flat arrow along local X, facing up. Grown by `pad` on every side for the outline.
        private static Mesh ArrowMesh(float length, float pad)
        {
            int key = Mathf.RoundToInt(length * 1000f) * 2 + (pad > 0f ? 1 : 0);
            if (Meshes.TryGetValue(key, out Mesh mesh)) return mesh;
            float half = length * 0.5f;
            float neck = half - HeadLength;
            float shaft = ShaftHalfWidth + pad;
            float head = HeadHalfWidth + pad * 1.6f;
            float tip = half + pad * 1.8f;
            float back = neck - pad * 0.7f;
            var vertices = new List<Vector3>
            {
                // Shaft between the two heads.
                new Vector3(-neck, 0f, -shaft), new Vector3(-neck, 0f, shaft),
                new Vector3(neck, 0f, shaft), new Vector3(neck, 0f, -shaft),
                // Right head, then left head.
                new Vector3(back, 0f, -head), new Vector3(back, 0f, head), new Vector3(tip, 0f, 0f),
                new Vector3(-back, 0f, head), new Vector3(-back, 0f, -head), new Vector3(-tip, 0f, 0f)
            };
            mesh = new Mesh { name = pad > 0f ? "Box Axis Arrow Edge" : "Box Axis Arrow" };
            mesh.SetVertices(vertices);
            // Clockwise seen from above so the faces point up.
            mesh.SetTriangles(new[] { 0, 1, 2, 0, 2, 3, 4, 5, 6, 7, 8, 9 }, 0);
            var normals = new Vector3[vertices.Count];
            for (int i = 0; i < normals.Length; i++) normals[i] = Vector3.up;
            mesh.normals = normals;
            mesh.RecalculateBounds();
            Meshes.Add(key, mesh);
            return mesh;
        }

        private static Material GetMaterial(ref Material material, Color color, string name)
        {
            if (material != null) return material;
            Shader shader = Shader.Find("Money Design/Soft Plastic")
                ?? Shader.Find("Universal Render Pipeline/Unlit")
                ?? Shader.Find("Unlit/Color");
            if (shader == null) return null;
            material = new Material(shader) { name = name, enableInstancing = true };
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
            if (material.HasProperty("_Gloss")) material.SetFloat("_Gloss", 0f);
            if (material.HasProperty("_Shade")) material.SetFloat("_Shade", 0f);
            if (material.HasProperty("_Cull")) material.SetFloat("_Cull", 0f);
            return material;
        }
    }
}
