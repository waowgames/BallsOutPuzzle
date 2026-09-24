using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace BallsOut
{
    // One shared mesh and one material per shape/color keep authored shapes cheap to render.
    internal static class BoxShapeVisual
    {
        private static readonly Dictionary<BoxShapeDefinition, Mesh> Meshes = new Dictionary<BoxShapeDefinition, Mesh>();

        internal static GameObject Create(BoxShapeDefinition shape, Material material, Transform parent, float cellSize)
        {
            if (!Meshes.TryGetValue(shape, out Mesh mesh))
            {
                mesh = BuildMesh(shape);
                Meshes.Add(shape, mesh);
            }

            var visual = new GameObject("Box Shape", typeof(MeshFilter), typeof(MeshRenderer));
            visual.transform.SetParent(parent, false);
            visual.transform.localScale = Vector3.one * cellSize;
            visual.GetComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = visual.GetComponent<MeshRenderer>();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.sharedMaterial = material;
            PrefabRegistry.ApplyMaterial(visual, material);
            return visual;
        }

        private static Mesh BuildMesh(BoxShapeDefinition shape)
        {
            const float edge = 0.49f;
            const float floor = 0.035f;
            const float wallTop = 0.34f;
            const float wallThickness = 0.08f;
            var vertices = new List<Vector3>(shape.CellCount * 80);
            var normals = new List<Vector3>(shape.CellCount * 80);
            var uv = new List<Vector2>(shape.CellCount * 80);
            var triangles = new List<int>(shape.CellCount * 120);
            var cells = new HashSet<Vector2Int>(shape.Cells);
            foreach (Vector2Int cell in shape.Cells)
            {
                float left = cell.x - edge, right = cell.x + edge;
                float near = cell.y - edge, far = cell.y + edge;
                // Keep the receiver open: settled and incoming balls render above the floor.
                AddFace(new Vector3(cell.x - 0.5f, floor, cell.y - 0.5f),
                    new Vector3(cell.x - 0.5f, floor, cell.y + 0.5f),
                    new Vector3(cell.x + 0.5f, floor, cell.y + 0.5f),
                    new Vector3(cell.x + 0.5f, floor, cell.y - 0.5f));
                if (!cells.Contains(cell + Vector2Int.left))
                    AddWall(left, left + wallThickness, near, far);
                if (!cells.Contains(cell + Vector2Int.right))
                    AddWall(right - wallThickness, right, near, far);
                if (!cells.Contains(cell + Vector2Int.down))
                    AddWall(left, right, near, near + wallThickness);
                if (!cells.Contains(cell + Vector2Int.up))
                    AddWall(left, right, far - wallThickness, far);
            }

            var mesh = new Mesh { name = "Box " + shape.name };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uv);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;

            void AddWall(float left, float right, float near, float far)
            {
                AddFace(new Vector3(left, wallTop, near), new Vector3(left, wallTop, far),
                    new Vector3(right, wallTop, far), new Vector3(right, wallTop, near));
                AddFace(new Vector3(left, floor, far), new Vector3(left, wallTop, far),
                    new Vector3(left, wallTop, near), new Vector3(left, floor, near));
                AddFace(new Vector3(right, floor, near), new Vector3(right, wallTop, near),
                    new Vector3(right, wallTop, far), new Vector3(right, floor, far));
                AddFace(new Vector3(left, floor, near), new Vector3(left, wallTop, near),
                    new Vector3(right, wallTop, near), new Vector3(right, floor, near));
                AddFace(new Vector3(right, floor, far), new Vector3(right, wallTop, far),
                    new Vector3(left, wallTop, far), new Vector3(left, floor, far));
            }

            void AddFace(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
            {
                int start = vertices.Count;
                Vector3 normal = Vector3.Cross(b - a, c - a).normalized;
                vertices.Add(a); vertices.Add(b); vertices.Add(c); vertices.Add(d);
                for (int i = 0; i < 4; i++) normals.Add(normal);
                uv.Add(new Vector2(0f, 0f)); uv.Add(new Vector2(0f, 1f));
                uv.Add(new Vector2(1f, 1f)); uv.Add(new Vector2(1f, 0f));
                triangles.Add(start); triangles.Add(start + 1); triangles.Add(start + 2);
                triangles.Add(start); triangles.Add(start + 2); triangles.Add(start + 3);
            }
        }
    }
}
