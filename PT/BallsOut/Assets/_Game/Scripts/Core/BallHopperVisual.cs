using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace BallsOut
{
    // The reservoir, funnel and lower frame share one surface and five shared materials.
    [ExecuteAlways, RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class BallHopperVisual : MonoBehaviour
    {
        private const int FrameFloorMaterial = 0;
        private const int DepotFloorMaterial = 4;
        private const float NeckRadius = 0.42f;
        private const int NeckSteps = 10;
        private const string ShadowName = "Board Shadow";
        private const float ShadowPixelsPerCell = 12f;
        // Between the desk top (-0.33) and the board floor, so the board hides the blob's core.
        private const float ShadowHeight = -0.2f;
        [SerializeField] private LevelDefinition level;
        [SerializeField] private Material shadowMaterial;
        [Tooltip("Drop shadow shift in macro cells; negative Y falls toward the bottom of the screen.")]
        [SerializeField] private Vector2 shadowOffset = new Vector2(0.06f, -0.32f);
        [SerializeField, Range(0.05f, 1f)] private float shadowSoftness = 0.34f;
        private Mesh generatedMesh;
        private float shoulderLeft, shoulderRight;
        private readonly List<Vector3> vertices = new List<Vector3>();
        private readonly List<Vector3> normals = new List<Vector3>();
        private readonly List<BoardRimMesh.Edge> rimEdges = new List<BoardRimMesh.Edge>();
        private readonly List<Vector3> neckOutline = new List<Vector3>();
        private readonly List<int>[] triangles =
            { new List<int>(), new List<int>(), new List<int>(), new List<int>(), new List<int>() };

        public void Initialize(LevelDefinition definition)
        {
            level = definition;
            Rebuild();
        }

        private void OnEnable() => Rebuild();
        private void OnDisable() => ReleaseMesh();
        private void OnDestroy() => SoftShadowMask.Release(ShadowRenderer);

        private MeshRenderer ShadowRenderer
        {
            get
            {
                Transform shadow = transform.Find(ShadowName);
                return shadow != null ? shadow.GetComponent<MeshRenderer>() : null;
            }
        }

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
            MeshRenderer shadow = ShadowRenderer;
            if (level == null || level.macroGridWidth < 1 || level.lowerGridHeight < 1 ||
                level.ballAreaMacroHeight < 1 || level.macroCellSize <= 0f)
            {
                if (shadow != null) shadow.gameObject.SetActive(false);
                return;
            }
            // Retire the old rectangular art so only the generated board is visible.
            foreach (Transform child in transform)
                if (child.name != ShadowName) child.gameObject.SetActive(false);
            Transform oldFrame = transform.parent != null ? transform.parent.Find("PF_BoardFrame") : null;
            if (oldFrame != null) oldFrame.gameObject.SetActive(false);
            vertices.Clear();
            normals.Clear();
            rimEdges.Clear();
            foreach (var submesh in triangles) submesh.Clear();
            if (level.hopperMicroRows > 0) PlanNeck();
            // Grid, reservoir and funnel share one base height, so their outline is a single unbroken rail.
            BuildLowerFrame();
            BuildReservoir();
            if (level.hopperMicroRows > 0) BuildHopper();
            BoardRimMesh.Append(rimEdges, BoardRimMesh.Frame, level.macroCellSize, vertices, normals, triangles);
            // The divider is swept on its own so its ends tuck into the side rails instead of rerouting them.
            rimEdges.Clear();
            BuildDivider();
            BoardRimMesh.Append(rimEdges, BoardRimMesh.Bar, level.macroCellSize, vertices, normals, triangles);
            rimEdges.Clear();
            BuildReservoirDividers();
            BoardRimMesh.Append(rimEdges, BoardRimMesh.Slat, level.macroCellSize, vertices, normals, triangles);
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
            BuildShadow(shadow);
        }

        // The finished board mesh is its own silhouette, rim corners and funnel included.
        private void BuildShadow(MeshRenderer shadow)
        {
            if (shadowMaterial == null)
            {
                if (shadow != null) shadow.gameObject.SetActive(false);
                return;
            }
            float s = level.macroCellSize;
            Vector2 min = Vector2.positiveInfinity, max = Vector2.negativeInfinity;
            foreach (Vector3 v in vertices)
            {
                min = Vector2.Min(min, new Vector2(v.x, v.z));
                max = Vector2.Max(max, new Vector2(v.x, v.z));
            }
            var mask = new SoftShadowMask(Rect.MinMaxRect(min.x, min.y, max.x, max.y),
                ShadowPixelsPerCell / s, shadowSoftness * s);
            foreach (var submesh in triangles)
                for (int i = 0; i < submesh.Count; i += 3)
                    mask.AddTriangle(XZ(vertices[submesh[i]]), XZ(vertices[submesh[i + 1]]), XZ(vertices[submesh[i + 2]]));
            if (shadow == null) shadow = SoftShadowMask.CreateRenderer(transform, shadowMaterial, ShadowName);
            shadow.sharedMaterial = shadowMaterial;
            shadow.gameObject.SetActive(true);
            mask.ApplyTo(shadow, ShadowHeight, shadowOffset * s, 1f);
        }

        private static Vector2 XZ(Vector3 p) => new Vector2(p.x, p.z);

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
                    Floor(FrameFloorMaterial, left, right, bottom, top, 0.025f * s);
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
            bool hopper = level.hopperMicroRows > 0;
            int endRow = hopper ? level.HopperStartRow : level.ballAreaMacroHeight * LevelDefinition.MicroResolution;
            for (int y = 0; y * LevelDefinition.MicroResolution < endRow; y++)
                for (int x = 0; x < level.macroGridWidth; x++)
                {
                    if (!Depot(x, y)) continue;
                    float left = x * s, right = left + s;
                    float bottom = y == 0 ? level.DepotBottom : level.BallRowZ(y * LevelDefinition.MicroResolution - 0.5f);
                    int nextRow = Mathf.Min((y + 1) * LevelDefinition.MicroResolution, endRow);
                    float top = level.BallRowZ(nextRow - 0.5f);
                    Floor(DepotFloorMaterial, left, right, bottom, top, level.DepotFloorHeight);
                    if (!Depot(x - 1, y)) Rail(P(left, bottom), P(left, top));
                    if (!Depot(x + 1, y)) Rail(P(right, top), P(right, bottom));
                    bool joinsGrid = y == 0 && Lower(x, level.lowerGridHeight - 1);
                    if (!Depot(x, y - 1) && !joinsGrid) Rail(P(right, bottom), P(left, bottom));
                    if (hopper && nextRow == endRow)
                    {
                        // Shoulders are walled up to the rounded neck, which the funnel outline continues.
                        if (left < shoulderLeft) Rail(P(left, top), P(Mathf.Min(right, shoulderLeft), top));
                        if (right > shoulderRight) Rail(P(Mathf.Max(left, shoulderRight), top), P(right, top));
                    }
                    else if (!Depot(x, y + 1)) Rail(P(left, top), P(right, top));
                }
        }

        private float Center => level.macroGridWidth * level.macroCellSize * 0.5f;
        private Vector3 Mirror(Vector3 p) => new Vector3(2f * Center - p.x, p.y, p.z);

        // Rounds the concave neck so the funnel wall flows into the shoulder and its rail
        // stays clear of the reservoir balls below.
        private void PlanNeck()
        {
            float bottom = level.BallRowZ(level.HopperStartRow - 0.5f);
            float neck = level.HopperHalfWidth(level.HopperStartRow - 0.5f);
            float mouth = level.HopperHalfWidth(level.ballAreaMacroHeight * LevelDefinition.MicroResolution - 0.5f);
            Vector3 a = P(Center - neck, bottom), d = P(Center - mouth, level.DepotTop);
            Vector3 wall = (d - a).normalized;
            float half = Vector3.Angle(Vector3.left, wall) * Mathf.Deg2Rad * 0.5f;
            float reach = Mathf.Min(NeckRadius * level.macroCellSize / Mathf.Tan(half),
                0.45f * Mathf.Min((d - a).magnitude, Center - neck));
            float radius = reach * Mathf.Tan(half);
            Vector3 start = a + Vector3.left * reach, end = a + wall * reach;
            Vector3 pivot = a + (Vector3.left + wall).normalized * (radius / Mathf.Sin(half));
            float sweep = Vector3.SignedAngle(start - pivot, end - pivot, Vector3.up);
            neckOutline.Clear();
            neckOutline.Add(a);
            for (int i = 0; i <= NeckSteps; i++)
                neckOutline.Add(pivot + Quaternion.AngleAxis(sweep * i / NeckSteps, Vector3.up) * (start - pivot));
            neckOutline.Add(d);
            shoulderLeft = start.x;
            shoulderRight = Mirror(start).x;
        }

        private void BuildHopper()
        {
            // neckOutline: [neck corner, rounded neck..., mouth corner] for the left wall.
            Vector3 lift = Vector3.up * level.DepotFloorHeight;
            Vector3 a = neckOutline[0], d = neckOutline[neckOutline.Count - 1];
            Quad(DepotFloorMaterial, a + lift, Mirror(a) + lift, Mirror(d) + lift, d + lift);
            for (int i = 1; i < neckOutline.Count - 2; i++)
            {
                Triangle(DepotFloorMaterial, a + lift, neckOutline[i] + lift, neckOutline[i + 1] + lift);
                Triangle(DepotFloorMaterial, Mirror(a) + lift, Mirror(neckOutline[i]) + lift, Mirror(neckOutline[i + 1]) + lift);
            }
            for (int i = 1; i < neckOutline.Count - 1; i++) Rail(neckOutline[i], neckOutline[i + 1]);
            Rail(d, Mirror(d));
            for (int i = neckOutline.Count - 1; i > 1; i--) Rail(Mirror(neckOutline[i]), Mirror(neckOutline[i - 1]));
        }

        private void BuildDivider()
        {
            float s = level.macroCellSize;
            float z = level.DepotBottom;
            // Ends stop mid-frame so each cap hides inside it as a clean T.
            float endExtension = 0.15f * s;
            // The bar's lip faces the grid, so its body sits on the reservoir side of the seam.
            for (int x = 0; x < level.macroGridWidth; x++)
            {
                if (!Lower(x, level.lowerGridHeight - 1) || !Depot(x, 0)) continue;
                bool joinsLeft = Lower(x - 1, level.lowerGridHeight - 1) && Depot(x - 1, 0);
                bool joinsRight = Lower(x + 1, level.lowerGridHeight - 1) && Depot(x + 1, 0);
                float left = x * s - (joinsLeft ? 0f : endExtension);
                float right = (x + 1) * s + (joinsRight ? 0f : endExtension);
                Rail(P(left, z), P(right, z));
            }
        }

        private void BuildReservoirDividers()
        {
            if (level.reservoirDividerColumns == null) return;
            float size = level.macroCellSize;
            // Centred on the column boundary; both ends run into the mid-line of the bar and top frame.
            foreach (int column in level.reservoirDividerColumns)
            {
                float x = column * size;
                Rail(P(x, level.DepotBottom + 0.15f * size), P(x, level.DepotTop + 0.15f * size));
            }
        }

        private static Vector3 P(float x, float z) => new Vector3(x, 0f, z);

        private void Floor(int material, float left, float right, float bottom, float top, float height)
        {
            Vector3 lift = Vector3.up * height;
            Quad(material, P(left, bottom) + lift, P(right, bottom) + lift,
                P(right, top) + lift, P(left, top) + lift);
        }

        private void Rail(Vector3 from, Vector3 to)
        {
            rimEdges.Add(new BoardRimMesh.Edge(from, to));
        }

        private void Quad(int material, Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            Triangle(material, a, b, c);
            Triangle(material, a, c, d);
        }

        private void Triangle(int material, Vector3 a, Vector3 b, Vector3 c)
        {
            int start = vertices.Count;
            vertices.Add(a); vertices.Add(b); vertices.Add(c);
            for (int i = 0; i < 3; i++) normals.Add(Vector3.up);
            var indices = triangles[material];
            // Unity treats clockwise-from-above as front facing.
            bool upward = Vector3.Cross(b - a, c - a).y >= 0f;
            indices.Add(start); indices.Add(upward ? start + 1 : start + 2); indices.Add(upward ? start + 2 : start + 1);
        }
    }
}
