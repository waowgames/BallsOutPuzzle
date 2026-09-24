using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace BallsOut
{
    // The reservoir, funnel and lower frame share one surface and four shared materials.
    [ExecuteAlways, RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class BallHopperVisual : MonoBehaviour
    {
        [SerializeField] private LevelDefinition level;
        private Mesh generatedMesh;
        private readonly List<Vector3> vertices = new List<Vector3>();
        private readonly List<Vector3> normals = new List<Vector3>();
        private readonly List<BoardRimMesh.Edge> rimEdges = new List<BoardRimMesh.Edge>();
        private readonly List<int>[] triangles = { new List<int>(), new List<int>(), new List<int>(), new List<int>() };

        public void Initialize(LevelDefinition definition)
        {
            level = definition;
            Rebuild();
        }

        private void OnEnable() => Rebuild();
        private void OnDisable() => ReleaseMesh();

        private void ReleaseMesh()
        {
            if (generatedMesh == null) return;
            GetComponent<MeshFilter>().sharedMesh = null;
            if (Application.isPlaying) Destroy(generatedMesh);
            else DestroyImmediate(generatedMesh);
            generatedMesh = null;
        }

        private void Rebuild()
        {
            ReleaseMesh();
            if (level == null || level.macroGridWidth < 1 || level.lowerGridHeight < 1 ||
                level.ballAreaMacroHeight < 1 || level.macroCellSize <= 0f) return;
            // Retire the old rectangular art so only the generated board is visible.
            foreach (Transform child in transform) child.gameObject.SetActive(false);
            Transform oldFrame = transform.parent != null ? transform.parent.Find("PF_BoardFrame") : null;
            if (oldFrame != null) oldFrame.gameObject.SetActive(false);
            vertices.Clear();
            normals.Clear();
            rimEdges.Clear();
            foreach (var submesh in triangles) submesh.Clear();
            BuildLowerFrame();
            BuildReservoir();
            if (level.hopperMicroRows > 0) BuildHopper();
            BuildDivider();
            BoardRimMesh.Append(rimEdges, level.macroCellSize, vertices, normals, triangles);
            generatedMesh = new Mesh { name = "Level Board Surface", hideFlags = HideFlags.DontSave };
            if (vertices.Count > 65535) generatedMesh.indexFormat = IndexFormat.UInt32;
            generatedMesh.SetVertices(vertices);
            generatedMesh.subMeshCount = triangles.Length;
            for (int i = 0; i < triangles.Length; i++) generatedMesh.SetTriangles(triangles[i], i);
            generatedMesh.SetNormals(normals);
            generatedMesh.RecalculateBounds();
            GetComponent<MeshFilter>().sharedMesh = generatedMesh;
            var renderer = GetComponent<MeshRenderer>();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        private bool Lower(int x, int y) => level.lowerGridMask.Get(new Vector2Int(x, y),
            level.macroGridWidth, level.lowerGridHeight) != CellKind.Outside;

        private bool Depot(int x, int y) => level.ballAreaMask.Get(new Vector2Int(x, y),
            level.macroGridWidth, level.ballAreaMacroHeight) == CellKind.Usable;

        private void BuildLowerFrame()
        {
            float s = level.macroCellSize;
            for (int y = 0; y < level.lowerGridHeight; y++)
                for (int x = 0; x < level.macroGridWidth; x++)
                {
                    if (!Lower(x, y)) continue;
                    float left = x * s, right = left + s, bottom = y * s, top = bottom + s;
                    Floor(left, right, bottom, top, 0.025f * s);
                    if (!Lower(x - 1, y)) Rail(P(left, bottom), P(left, top));
                    if (!Lower(x + 1, y)) Rail(P(right, top), P(right, bottom));
                    if (!Lower(x, y - 1)) Rail(P(right, bottom), P(left, bottom));
                    bool joinsDepot = y == level.lowerGridHeight - 1 && Depot(x, 0);
                    if (!Lower(x, y + 1) && !joinsDepot) Rail(P(left, top), P(right, top));
                }
        }

        private void BuildReservoir()
        {
            float s = level.macroCellSize;
            int endRow = level.hopperMicroRows > 0 ? level.HopperStartRow : level.ballAreaMacroHeight * LevelDefinition.MicroResolution;
            for (int y = 0; y * LevelDefinition.MicroResolution < endRow; y++)
                for (int x = 0; x < level.macroGridWidth; x++)
                {
                    if (!Depot(x, y)) continue;
                    float left = x * s, right = left + s;
                    float bottom = y == 0 ? level.DepotBottom : level.BallRowZ(y * LevelDefinition.MicroResolution - 0.5f);
                    int nextRow = Mathf.Min((y + 1) * LevelDefinition.MicroResolution, endRow);
                    float top = level.BallRowZ(nextRow - 0.5f);
                    Floor(left, right, bottom, top, level.DepotElevation + 0.13f * s);
                    if (!Depot(x - 1, y)) Rail(Raised(left, bottom), Raised(left, top));
                    if (!Depot(x + 1, y)) Rail(Raised(right, top), Raised(right, bottom));
                    bool joinsGrid = y == 0 && Lower(x, level.lowerGridHeight - 1);
                    if (!Depot(x, y - 1) && !joinsGrid) Rail(Raised(right, bottom), Raised(left, bottom));
                    // The funnel entrance is deliberately wall-free, including both shoulders.
                    bool joinsHopper = level.hopperMicroRows > 0 && nextRow == endRow;
                    if (!Depot(x, y + 1) && !joinsHopper) Rail(Raised(left, top), Raised(right, top));
                }
        }

        private void BuildHopper()
        {
            float center = level.macroGridWidth * level.macroCellSize * 0.5f;
            float bottom = level.BallRowZ(level.HopperStartRow - 0.5f);
            float top = level.DepotTop;
            float neck = level.HopperHalfWidth(level.HopperStartRow - 0.5f);
            float mouth = level.HopperHalfWidth(level.ballAreaMacroHeight * LevelDefinition.MicroResolution - 0.5f);
            Vector3 a = Raised(center - neck, bottom), b = Raised(center + neck, bottom);
            Vector3 c = Raised(center + mouth, top), d = Raised(center - mouth, top);
            Vector3 lift = Vector3.up * (0.13f * level.macroCellSize);
            Quad(0, a + lift, b + lift, c + lift, d + lift);
            Rail(a, d);
            Rail(c, b);
            Rail(d, c);
        }

        private void BuildDivider()
        {
            float s = level.macroCellSize;
            float z = level.DepotBottom;
            float endExtension = 0.34f * s;
            // The rail faces the grid, so its rounded body sits on the reservoir side of the seam.
            for (int x = 0; x < level.macroGridWidth; x++)
            {
                if (!Lower(x, level.lowerGridHeight - 1) || !Depot(x, 0)) continue;
                bool joinsLeft = Lower(x - 1, level.lowerGridHeight - 1) && Depot(x - 1, 0);
                bool joinsRight = Lower(x + 1, level.lowerGridHeight - 1) && Depot(x + 1, 0);
                float left = x * s - (joinsLeft ? 0f : endExtension);
                float right = (x + 1) * s + (joinsRight ? 0f : endExtension);
                Rail(new Vector3(left, 0.08f * s, z), new Vector3(right, 0.08f * s, z));
            }
        }

        private static Vector3 P(float x, float z) => new Vector3(x, 0f, z);
        private Vector3 Raised(float x, float z) => new Vector3(x, level.DepotElevation, z);

        private void Floor(float left, float right, float bottom, float top, float height)
        {
            Vector3 lift = Vector3.up * height;
            Quad(0, P(left, bottom) + lift, P(right, bottom) + lift,
                P(right, top) + lift, P(left, top) + lift);
        }

        private void Rail(Vector3 from, Vector3 to)
        {
            rimEdges.Add(new BoardRimMesh.Edge(from, to));
        }

        private void Quad(int material, Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            int start = vertices.Count;
            vertices.Add(a); vertices.Add(b); vertices.Add(c); vertices.Add(d);
            for (int i = 0; i < 4; i++) normals.Add(Vector3.up);
            var indices = triangles[material];
            if (Vector3.Cross(b - a, c - a).y >= 0f)
            {
                indices.Add(start); indices.Add(start + 1); indices.Add(start + 2);
                indices.Add(start); indices.Add(start + 2); indices.Add(start + 3);
            }
            else
            {
                indices.Add(start); indices.Add(start + 2); indices.Add(start + 1);
                indices.Add(start); indices.Add(start + 3); indices.Add(start + 2);
            }
        }
    }
}
