using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

namespace BallsOut
{
    // Procedural stone block over an obstacle's cells: a rounded slab with a chiselled frame
    // around a cobble field, a recessed seal holding the counter, cracks that spread as the count
    // falls, and a crumble into bouncing rubble and dust. Shares the ice block's slab shader.
    // Pure presentation; BoardObstacleSystem owns the count.
    internal sealed class ObstacleVisual : MonoBehaviour
    {
        private const float PunchDuration = 0.32f;
        private const float ShakeDuration = 0.32f;
        private const float HitFlashDuration = 0.2f;
        private const float BreakDuration = 0.16f;
        private const float LabelFadeDuration = 0.4f;
        private const int MaxCrackStage = 3;

        // Footprint, in cells: gap to neighbouring cells, corner radius, frame and cobble sizes.
        private const float Inset = 0.045f;
        private const float CornerRadius = 0.17f;
        private const float FrameWidth = 0.12f;
        private const float CobbleSpacing = 0.29f;
        private const float DistanceRange = 0.3f;
        private const float Padding = 0.12f;
        private const int PixelsPerCell = 64;
        private const float LayerStep = 0.02f;
        // Heights over the board, in cells: from the floor tiles to just under a box's lid.
        private const float BottomHeight = 0.2f;
        private const float TopHeight = 0.74f;

        private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int FlashId = Shader.PropertyToID("_Flash");
        private static readonly int RangeId = Shader.PropertyToID("_Range");
        private static readonly int ZWriteId = Shader.PropertyToID("_ZWrite");

        // Top-left light, as seen from the board camera (screen up is local +Z).
        private static readonly Vector2 Light2 = new Vector2(-0.55f, 0.83f).normalized;
        private static readonly Vector3 Light3 = new Vector3(-0.35f, 1f, 0.25f).normalized;

        // Authored in sRGB: cool slate with a lilac cast, so it reads as terrain beside the candy boxes.
        private static readonly Color FrameLow = new Color32(0x7A, 0x82, 0x98, 0xFF);
        private static readonly Color FrameHigh = new Color32(0x9C, 0xA4, 0xB9, 0xFF);
        private static readonly Color CobbleDark = new Color32(0x6B, 0x73, 0x8A, 0xFF);
        private static readonly Color CobbleLight = new Color32(0xA6, 0xAE, 0xC2, 0xFF);
        private static readonly Color Mortar = new Color32(0x43, 0x49, 0x5C, 0xFF);
        private static readonly Color RimLight = new Color32(0xE2, 0xE7, 0xF1, 0xFF);
        private static readonly Color RimShade = new Color32(0x3A, 0x40, 0x53, 0xFF);
        private static readonly Color SealLow = new Color32(0x2C, 0x31, 0x40, 0xFF);
        private static readonly Color SealHigh = new Color32(0x3E, 0x45, 0x58, 0xFF);
        private static readonly Color WallTop = new Color32(0x74, 0x7C, 0x92, 0xFF);
        private static readonly Color WallBottom = new Color32(0x3A, 0x40, 0x52, 0xFF);
        private static readonly Color Strata = new Color32(0x58, 0x5F, 0x74, 0xFF);
        private static readonly Color CrackCore = new Color32(0x23, 0x27, 0x33, 0xFF);
        private static readonly Color CrackLip = new Color32(0xC9, 0xCF, 0xDC, 0xFF);
        private static readonly Color Dust = new Color32(0xD9, 0xDD, 0xE7, 0xFF);
        private static readonly Color LastCount = new Color32(0xFF, 0xD6, 0x5A, 0xFF);
        private static Material material;
        private static Material dustMaterial;
        private static Material labelMaterial;

        private struct Crack
        {
            public Vector2 a, b;
            public float widthA, widthB;
            public int stage;
        }

        private struct Chunk
        {
            public Vector3 position, velocity, spin, size;
            public Quaternion rotation;
            public Color color;
            public float age, lifetime;
        }

        private struct Puff
        {
            public Vector3 position, velocity;
            public float radius, growth, alpha, age, lifetime;
        }

        private readonly List<Vector3> vertices = new List<Vector3>();
        private readonly List<Color> colors = new List<Color>();
        private readonly List<Vector2> uvs = new List<Vector2>();
        private readonly List<Vector2> layers = new List<Vector2>();
        private readonly List<int> triangles = new List<int>();
        private readonly List<Crack> cracks = new List<Crack>();
        private readonly List<Chunk> chunks = new List<Chunk>();
        private readonly List<Puff> puffs = new List<Puff>();
        private Vector2 extent;
        private Vector2 half;
        private float sealRadius;
        private float cellSize;
        private float bottom;
        private float top;
        private int cellCount;
        private int initialCount;
        private Rect bounds;
        private Vector2[] features;
        private float[] tones;
        private int featureColumns;
        private int featureRows;
        private Texture2D texture;
        private Color32[] pixels;
        private Color[] basePixels;
        private float[] crackShade;
        private float[] crackLip;
        private float[] crackCore;
        private int bakedCrackStage;
        private int crackStage;
        private Transform body;
        private Mesh bodyMesh;
        private MeshRenderer bodyRenderer;
        private Mesh rubbleMesh;
        private MeshRenderer rubbleRenderer;
        private Mesh dustMesh;
        private MeshRenderer dustRenderer;
        private Transform shadowPivot;
        private MeshRenderer shadow;
        private MaterialPropertyBlock properties;
        private TextMeshPro label;
        private Vector3 labelRest;
        private float labelScale;
        private System.Random random;
        private float punch = -1f;
        private float shake = -1f;
        private float hitFlash = -1f;
        private float breakTime = -1f;
        private float labelFade = -1f;
        private bool shattered;

        internal static ObstacleVisual Create(BoardGrid board, PrefabRegistry registry, RectInt area, int count)
        {
            var root = new GameObject($"Obstacle {area.x},{area.y}");
            root.transform.SetParent(board.Root, false);
            root.transform.localPosition = new Vector3(area.center.x, 0f, area.center.y) * board.CellSize;
            var visual = root.AddComponent<ObstacleVisual>();
            visual.Build(board.CellSize, registry, area, count);
            return visual;
        }

        private void Build(float size, PrefabRegistry registry, RectInt area, int count)
        {
            cellSize = size;
            extent = new Vector2(area.width, area.height);
            half = extent * 0.5f - Vector2.one * Inset;
            cellCount = area.width * area.height;
            initialCount = count;
            sealRadius = Mathf.Min(0.3f, Mathf.Min(half.x, half.y) - FrameWidth - 0.05f);
            bottom = BottomHeight * size;
            top = TopHeight * size;
            random = new System.Random(area.x * 7919 + area.y * 104729 + area.width * 31 + area.height);
            bounds = Rect.MinMaxRect(-extent.x * 0.5f - Padding, -extent.y * 0.5f - Padding,
                extent.x * 0.5f + Padding, extent.y * 0.5f + Padding);
            PlaceCobbles();

            if (material == null)
            {
                Shader shader = Shader.Find("Balls Out/Ice Box") ?? Shader.Find("Sprites/Default");
                material = new Material(shader) { name = "Stone Obstacle" };
                // Soft dust must not hide the rubble drawn after it.
                dustMaterial = new Material(shader) { name = "Stone Dust", renderQueue = material.renderQueue + 1 };
                if (dustMaterial.HasProperty(ZWriteId)) dustMaterial.SetFloat(ZWriteId, 0f);
            }
            texture = new Texture2D(Mathf.CeilToInt(bounds.width * PixelsPerCell), Mathf.CeilToInt(bounds.height * PixelsPerCell),
                TextureFormat.RGBA32, false)
            {
                name = "Obstacle Top",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            pixels = new Color32[texture.width * texture.height];
            basePixels = new Color[pixels.Length];
            crackShade = new float[pixels.Length];
            crackLip = new float[pixels.Length];
            crackCore = new float[pixels.Length];
            properties = new MaterialPropertyBlock();
            properties.SetTexture(MainTexId, texture);
            properties.SetFloat(RangeId, DistanceRange);
            properties.SetColor(ColorId, Color.white);
            properties.SetFloat(FlashId, 0f);

            if (registry != null && registry.shadowMaterial != null) CreateShadow(registry);

            body = new GameObject("Stone Body", typeof(MeshFilter), typeof(MeshRenderer)).transform;
            body.SetParent(transform, false);
            bodyMesh = new Mesh { name = "Stone Body" };
            body.GetComponent<MeshFilter>().sharedMesh = bodyMesh;
            bodyRenderer = SetupRenderer(body.GetComponent<MeshRenderer>(), material);
            bodyRenderer.SetPropertyBlock(properties);
            BuildBodyMesh();

            rubbleMesh = CreateParticleMesh("Stone Rubble", material, out rubbleRenderer);
            dustMesh = CreateParticleMesh("Stone Dust", dustMaterial, out dustRenderer);

            for (int stage = 1; stage <= MaxCrackStage; stage++) GenerateCracks(stage);
            CreateLabel();
            SetLabel(count);
            BakeBase();
            BakeCracks();
        }

        private static MeshRenderer SetupRenderer(MeshRenderer renderer, Material shared)
        {
            renderer.sharedMaterial = shared;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return renderer;
        }

        private Mesh CreateParticleMesh(string name, Material shared, out MeshRenderer renderer)
        {
            var particles = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            particles.transform.SetParent(transform, false);
            var mesh = new Mesh { name = name };
            mesh.MarkDynamic();
            particles.GetComponent<MeshFilter>().sharedMesh = mesh;
            renderer = SetupRenderer(particles.GetComponent<MeshRenderer>(), shared);
            renderer.enabled = false;
            return mesh;
        }

        // Spread cracks over the block's life: the last stage lands just before the final chip.
        private int StageFor(int count) => initialCount <= 1 ? 0
            : Mathf.Clamp((initialCount - count) * MaxCrackStage / (initialCount - 1), 0, MaxCrackStage);

        internal void SetCount(int count)
        {
            if (shattered) return;
            if (count <= 0)
            {
                shattered = true;
                breakTime = 0f;
                punch = -1f;
                hitFlash = -1f;
                shake = 0f;
                return;
            }
            SetLabel(count);
            punch = 0f;
            shake = 0f;
            hitFlash = 0f;
            SpawnRubble(4 + cellCount * 2, 0.55f, false);
            SpawnPuffs(2 + cellCount, 0.6f, false);
            int stage = StageFor(count);
            if (stage == crackStage) return;
            crackStage = stage;
            BakeCracks();
        }

        private void SetLabel(int count)
        {
            if (label == null) return;
            label.text = count.ToString();
            // The last chip is close: the number warms up.
            label.color = count == 1 && initialCount > 1 ? LastCount : Color.white;
        }

        private void Update()
        {
            float dt = Mathf.Min(Time.deltaTime, 1f / 20f);
            Vector3 offset = Vector3.zero;
            if (shake >= 0f)
            {
                shake += dt;
                float k = Mathf.Clamp01(shake / ShakeDuration);
                offset.x = Mathf.Sin(shake * 62f) * cellSize * 0.04f * (1f - k) * (1f - k);
                if (k >= 1f) shake = -1f;
            }
            if (body.gameObject.activeSelf) body.localPosition = offset;
            if (label != null && labelFade < 0f) label.transform.localPosition = labelRest + offset;
            if (punch >= 0f && label != null)
            {
                punch += dt;
                float k = Mathf.Clamp01(punch / PunchDuration);
                label.transform.localScale = Vector3.one * (labelScale * (1f + Mathf.Sin(k * Mathf.PI) * 0.45f));
                if (k >= 1f) punch = -1f;
            }
            if (hitFlash >= 0f)
            {
                hitFlash += dt;
                float k = Mathf.Clamp01(hitFlash / HitFlashDuration);
                properties.SetFloat(FlashId, 0.32f * (1f - k) * (1f - k));
                bodyRenderer.SetPropertyBlock(properties);
                if (k >= 1f) hitFlash = -1f;
            }
            if (breakTime >= 0f && body.gameObject.activeSelf) AdvanceBreak(dt);
            if (labelFade >= 0f) AdvanceLabelFade(dt);
            if (chunks.Count != 0) AdvanceRubble(dt);
            if (puffs.Count != 0) AdvancePuffs(dt);
            if (shattered && !body.gameObject.activeSelf && chunks.Count == 0 && puffs.Count == 0 && labelFade < 0f)
                Destroy(gameObject);
        }

        // A quick swell and white-out, then the block bursts into rubble and dust.
        private void AdvanceBreak(float dt)
        {
            breakTime += dt;
            float k = Mathf.Clamp01(breakTime / BreakDuration);
            body.localScale = new Vector3(1f + k * 0.07f, 1f - k * 0.05f, 1f + k * 0.07f);
            properties.SetFloat(FlashId, k * 0.8f);
            bodyRenderer.SetPropertyBlock(properties);
            if (shadowPivot != null) shadowPivot.localScale = Vector3.one * (1f - k * 0.3f);
            if (k < 1f) return;
            body.gameObject.SetActive(false);
            if (shadowPivot != null) shadowPivot.gameObject.SetActive(false);
            SpawnRubble(Mathf.Min(14 + cellCount * 10, 56), 1f, true);
            SpawnPuffs(Mathf.Min(5 + cellCount * 3, 17), 1f, true);
            labelFade = label != null ? 0f : -1f;
        }

        // The counter pops up off the rubble and fades.
        private void AdvanceLabelFade(float dt)
        {
            labelFade += dt;
            float k = Mathf.Clamp01(labelFade / LabelFadeDuration);
            float ease = 1f - (1f - k) * (1f - k);
            label.transform.localPosition = labelRest + Vector3.up * (cellSize * 0.35f * ease);
            label.transform.localScale = Vector3.one * (labelScale * (1f + ease * 0.7f));
            label.alpha = 1f - k;
            if (k < 1f) return;
            label.gameObject.SetActive(false);
            labelFade = -1f;
        }

        // ---- Footprint: a rounded rectangle centred on the root (cells) ----

        private float Distance(Vector2 p)
        {
            Vector2 q = new Vector2(Mathf.Abs(p.x), Mathf.Abs(p.y)) - half + Vector2.one * CornerRadius;
            return Vector2.Max(q, Vector2.zero).magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0f) - CornerRadius;
        }

        // Outward direction of the nearest outline point.
        private Vector2 OutlineNormal(Vector2 p)
        {
            Vector2 q = new Vector2(Mathf.Abs(p.x), Mathf.Abs(p.y)) - half + Vector2.one * CornerRadius;
            Vector2 n;
            if (q.x > 0f && q.y > 0f) n = q.normalized;
            else n = q.x > q.y ? Vector2.right : Vector2.up;
            return new Vector2(p.x < 0f ? -n.x : n.x, p.y < 0f ? -n.y : n.y);
        }

        // ---- Cobbles: a jittered lattice of stones, each with its own tone ----

        private void PlaceCobbles()
        {
            featureColumns = Mathf.CeilToInt(bounds.width / CobbleSpacing) + 1;
            featureRows = Mathf.CeilToInt(bounds.height / CobbleSpacing) + 1;
            features = new Vector2[featureColumns * featureRows];
            tones = new float[features.Length];
            for (int y = 0; y < featureRows; y++)
                for (int x = 0; x < featureColumns; x++)
                {
                    // Odd rows shift half a stone, so the field reads as laid stones, not a grid.
                    float shift = (y & 1) == 0 ? 0f : 0.5f;
                    features[y * featureColumns + x] = new Vector2(
                        bounds.xMin + (x + shift + Range(-0.22f, 0.22f)) * CobbleSpacing,
                        bounds.yMin + (y + 0.5f + Range(-0.22f, 0.22f)) * CobbleSpacing);
                    tones[y * featureColumns + x] = Range(0.2f, 1f);
                }
        }

        // Nearest stone's tone, the distance to its edge, and the direction to that edge.
        private void Cobble(Vector2 p, out float tone, out float edge, out Vector2 edgeDirection)
        {
            int gx = Mathf.FloorToInt((p.x - bounds.xMin) / CobbleSpacing);
            int gy = Mathf.FloorToInt((p.y - bounds.yMin) / CobbleSpacing);
            int nearest = -1;
            float best = float.MaxValue;
            for (int y = gy - 1; y <= gy + 1; y++)
                for (int x = gx - 1; x <= gx + 1; x++)
                {
                    if (x < 0 || y < 0 || x >= featureColumns || y >= featureRows) continue;
                    float distance = (features[y * featureColumns + x] - p).sqrMagnitude;
                    if (distance < best) { best = distance; nearest = y * featureColumns + x; }
                }
            tone = tones[nearest];
            Vector2 center = features[nearest];
            edge = float.MaxValue;
            edgeDirection = Vector2.up;
            int cx = nearest % featureColumns, cy = nearest / featureColumns;
            for (int y = cy - 2; y <= cy + 2; y++)
                for (int x = cx - 2; x <= cx + 2; x++)
                {
                    if (x < 0 || y < 0 || x >= featureColumns || y >= featureRows) continue;
                    int index = y * featureColumns + x;
                    if (index == nearest) continue;
                    Vector2 toOther = features[index] - center;
                    Vector2 direction = toOther.normalized;
                    // Distance to the bisector between the two stones.
                    float distance = Vector2.Dot(center + toOther * 0.5f - p, direction);
                    if (distance < edge) { edge = distance; edgeDirection = direction; }
                }
        }

        // ---- Baked top face: colour in RGB, signed distance in alpha ----

        private void BakeBase()
        {
            int width = texture.width;
            int height = texture.height;
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    Vector2 p = PixelToCells(x, y);
                    float coarse = Mathf.PerlinNoise(p.x * 2.6f + 41f, p.y * 2.6f + 7f);
                    float grain = Mathf.PerlinNoise(p.x * 9f + 3f, p.y * 9f + 19f);
                    float speck = Mathf.PerlinNoise(p.x * 26f + 11f, p.y * 26f + 5f);
                    // Chips in the silhouette; walls read the same distance, so they follow them.
                    float d = Distance(p) + (coarse - 0.5f) * 0.035f + (grain - 0.5f) * 0.012f;
                    float depth = -d;
                    float v = Mathf.Clamp01((p.y + extent.y * 0.5f) / extent.y);
                    Vector2 normal = OutlineNormal(p);

                    // Cobble field, recessed inside the frame.
                    Cobble(p, out float tone, out float edge, out Vector2 edgeDirection);
                    Color field = Color.Lerp(CobbleDark, CobbleLight, tone);
                    field = Color.Lerp(field, CobbleLight, Mathf.SmoothStep(0f, 1f, v) * 0.15f);
                    float bulge = 1f - Mathf.SmoothStep(0.012f, 0.075f, edge);
                    float lit = Vector2.Dot(edgeDirection, Light2);
                    field = lit > 0f ? Color.Lerp(field, RimLight, bulge * lit * 0.42f)
                        : Color.Lerp(field, RimShade, bulge * -lit * 0.38f);
                    field = Color.Lerp(field, Mortar, 1f - Mathf.SmoothStep(0.006f, 0.02f, edge));
                    // The frame shades the stones next to it.
                    field = Color.Lerp(field, Mortar, (1f - Mathf.SmoothStep(FrameWidth, FrameWidth + 0.07f, depth)) * 0.3f);

                    // Smooth frame with a chiselled outer bevel and an inner step down to the stones.
                    Color frame = Color.Lerp(FrameLow, FrameHigh, Mathf.SmoothStep(0f, 1f, v * 0.8f + 0.1f));
                    frame += new Color(1f, 1f, 1f, 0f) * ((grain - 0.5f) * 0.06f);
                    float outer = 1f - Mathf.SmoothStep(0f, 0.055f, depth);
                    float outerLit = Vector2.Dot(normal, Light2);
                    frame = outerLit > 0f ? Color.Lerp(frame, RimLight, outer * outerLit * 0.7f)
                        : Color.Lerp(frame, RimShade, outer * -outerLit * 0.6f);
                    float lip = Mathf.Clamp01(1f - Mathf.Abs(depth - 0.058f) / 0.012f) * Mathf.Clamp01(outerLit * 1.5f + 0.3f);
                    frame = Color.Lerp(frame, RimLight, lip * 0.35f);
                    float inner = Mathf.SmoothStep(FrameWidth - 0.035f, FrameWidth, depth);
                    float innerLit = -outerLit;
                    frame = innerLit > 0f ? Color.Lerp(frame, RimLight, inner * innerLit * 0.5f)
                        : Color.Lerp(frame, RimShade, inner * -innerLit * 0.55f);
                    frame = Color.Lerp(frame, RimShade, (1f - Mathf.SmoothStep(0f, 0.02f, depth)) * 0.7f);

                    float onField = Mathf.SmoothStep(FrameWidth - 0.004f, FrameWidth + 0.008f, depth);
                    Color color = Color.Lerp(frame, field, onField);
                    color = Color.Lerp(color, Mortar, Mathf.Clamp01((speck - 0.64f) * 3f) * 0.28f);
                    color = ApplySeal(color, p, v);

                    color.a = Mathf.Clamp01(0.5f - d / (2f * DistanceRange));
                    basePixels[y * width + x] = color;
                    pixels[y * width + x] = color;
                }
        }

        // A raised ring around a dark recessed disc: the counter sits on it and always reads.
        private Color ApplySeal(Color color, Vector2 p, float v)
        {
            float r = sealRadius;
            float s = p.magnitude;
            Vector2 radial = s > 1e-4f ? p / s : Vector2.up;
            if (s >= r) return Color.Lerp(color, Mortar, (1f - Mathf.SmoothStep(r, r + 0.06f, s)) * 0.4f);
            float ringLit = Vector2.Dot(radial, Light2);
            Color ring = Color.Lerp(FrameLow, FrameHigh, 0.35f + v * 0.5f);
            float outerSlope = Mathf.SmoothStep(r - 0.03f, r, s);
            ring = ringLit > 0f ? Color.Lerp(ring, RimLight, outerSlope * ringLit * 0.7f)
                : Color.Lerp(ring, RimShade, outerSlope * -ringLit * 0.6f);
            float innerSlope = 1f - Mathf.SmoothStep(r - 0.075f, r - 0.05f, s);
            ring = -ringLit > 0f ? Color.Lerp(ring, RimLight, innerSlope * -ringLit * 0.45f)
                : Color.Lerp(ring, RimShade, innerSlope * ringLit * 0.5f);
            Color disc = Color.Lerp(SealLow, SealHigh, Mathf.SmoothStep(0f, 1f, 1f - v));
            // The recess wall under the light stays in shade; the far wall catches it.
            float wall = Mathf.SmoothStep(r - 0.16f, r - 0.075f, s);
            disc = Color.Lerp(disc, ringLit > 0f ? SealLow * 0.7f : SealHigh * 1.25f, wall * Mathf.Abs(ringLit) * 0.7f);
            return Color.Lerp(disc, ring, Mathf.SmoothStep(r - 0.082f, r - 0.068f, s));
        }

        // Cracks: a soft dark halo, a lit lower-right lip and a dark core. Stages only add
        // cracks, so each call folds in the new ones and repaints just their area.
        private void BakeCracks()
        {
            int width = texture.width;
            int height = texture.height;
            int xMin = width, yMin = height, xMax = -1, yMax = -1;
            var lipOffset = new Vector2(0.013f, -0.013f);
            foreach (Crack crack in cracks)
            {
                if (crack.stage <= bakedCrackStage || crack.stage > crackStage) continue;
                float reach = Mathf.Max(crack.widthA, crack.widthB) * 2f + 0.02f;
                int x0 = Mathf.Max(0, Mathf.FloorToInt(PixelX(Mathf.Min(crack.a.x, crack.b.x) - reach)));
                int x1 = Mathf.Min(width - 1, Mathf.CeilToInt(PixelX(Mathf.Max(crack.a.x, crack.b.x) + reach)));
                int y0 = Mathf.Max(0, Mathf.FloorToInt(PixelY(Mathf.Min(crack.a.y, crack.b.y) - reach)));
                int y1 = Mathf.Min(height - 1, Mathf.CeilToInt(PixelY(Mathf.Max(crack.a.y, crack.b.y) + reach)));
                if (x0 > x1 || y0 > y1) continue;
                xMin = Mathf.Min(xMin, x0); xMax = Mathf.Max(xMax, x1);
                yMin = Mathf.Min(yMin, y0); yMax = Mathf.Max(yMax, y1);
                for (int y = y0; y <= y1; y++)
                    for (int x = x0; x <= x1; x++)
                    {
                        Vector2 p = PixelToCells(x, y);
                        int i = y * width + x;
                        SegmentDistance(p, crack, out float distance, out float crackWidth);
                        crackCore[i] = Mathf.Max(crackCore[i], 1f - Mathf.Clamp01((distance - crackWidth * 0.3f) / (crackWidth * 0.45f)));
                        crackShade[i] = Mathf.Max(crackShade[i], 1f - Mathf.Clamp01((distance - crackWidth) / (crackWidth * 1.2f)));
                        SegmentDistance(p - lipOffset, crack, out distance, out crackWidth);
                        crackLip[i] = Mathf.Max(crackLip[i], 1f - Mathf.Clamp01((distance - crackWidth * 0.25f) / (crackWidth * 0.4f)));
                    }
            }
            bakedCrackStage = crackStage;

            for (int y = yMin; y <= yMax; y++)
                for (int x = xMin; x <= xMax; x++)
                {
                    int i = y * width + x;
                    Color color = basePixels[i];
                    float alpha = color.a;
                    // Alpha encodes the distance; keep the silhouette's rim and the counter's seal clean.
                    float mask = Mathf.Clamp01((alpha - 0.5f) * (2f * DistanceRange / 0.04f));
                    mask *= Mathf.SmoothStep(sealRadius, sealRadius + 0.03f, PixelToCells(x, y).magnitude);
                    color = Color.Lerp(color, Mortar, crackShade[i] * 0.35f * mask);
                    color = Color.Lerp(color, CrackLip, crackLip[i] * (1f - crackCore[i]) * 0.5f * mask);
                    color = Color.Lerp(color, CrackCore, crackCore[i] * 0.92f * mask);
                    color.a = alpha;
                    pixels[i] = color;
                }
            texture.SetPixels32(pixels);
            texture.Apply(false);
        }

        private Vector2 PixelToCells(int x, int y) => new Vector2(bounds.xMin + (x + 0.5f) / texture.width * bounds.width,
            bounds.yMin + (y + 0.5f) / texture.height * bounds.height);
        private float PixelX(float cellX) => (cellX - bounds.xMin) / bounds.width * texture.width - 0.5f;
        private float PixelY(float cellY) => (cellY - bounds.yMin) / bounds.height * texture.height - 0.5f;

        private static void SegmentDistance(Vector2 p, Crack crack, out float distance, out float width)
        {
            Vector2 ab = crack.b - crack.a;
            float t = Mathf.Clamp01(Vector2.Dot(p - crack.a, ab) / Mathf.Max(ab.sqrMagnitude, 1e-6f));
            distance = (p - (crack.a + ab * t)).magnitude;
            width = Mathf.Lerp(crack.widthA, crack.widthB, t);
        }

        // Each stage adds branching fractures from impact points around the seal.
        private void GenerateCracks(int stage)
        {
            int impacts = 1 + cellCount / 2 + (stage == MaxCrackStage ? 1 : 0);
            for (int i = 0; i < impacts; i++)
            {
                var origin = new Vector2(Range(-half.x, half.x) * 0.7f, Range(-half.y, half.y) * 0.7f);
                // Start outside the seal, so the fractures run off it rather than across the number.
                if (origin.magnitude < sealRadius + 0.05f)
                    origin = (origin.sqrMagnitude > 1e-4f ? origin.normalized : Vector2.right) * (sealRadius + 0.05f);
                int rays = random.Next(2, 4);
                float baseAngle = Range(0f, Mathf.PI * 2f);
                for (int r = 0; r < rays; r++)
                {
                    float angle = baseAngle + r * Mathf.PI * 2f / rays + Range(-0.45f, 0.45f);
                    GrowCrack(origin, angle, random.Next(3, 6), 0.03f, stage, true);
                }
            }
        }

        private void GrowCrack(Vector2 point, float angle, int segments, float width, int stage, bool canBranch)
        {
            for (int s = 0; s < segments; s++)
            {
                angle += Range(-0.5f, 0.5f);
                Vector2 next = point + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * Range(0.08f, 0.14f);
                if (Distance(next) > -0.05f) return;
                float nextWidth = Mathf.Max(width * 0.75f, 0.01f);
                cracks.Add(new Crack { a = point, b = next, widthA = width, widthB = nextWidth, stage = stage });
                if (canBranch && s == 1 && random.NextDouble() < 0.6)
                    GrowCrack(next, angle + (random.NextDouble() < 0.5 ? -0.95f : 0.95f), 2, nextWidth, stage, false);
                point = next;
                width = nextWidth;
            }
        }

        // ---- Slab mesh: stacked outline layers form soft rounded walls ----

        private void BuildBodyMesh()
        {
            ClearBuffers();
            float height = top - bottom;
            int count = Mathf.Max(4, Mathf.CeilToInt(height / (cellSize * LayerStep)));
            float round = Mathf.Min(0.07f, height / cellSize * 0.3f);
            // Back to front so the antialiased layer edges blend into the solid stone below.
            for (int i = 0; i <= count; i++)
            {
                float f = (float)i / count;
                float y = Mathf.Lerp(bottom, top, f);
                float fromTop = (top - y) / cellSize;
                float fromBottom = (y - bottom) / cellSize;
                float shrink = 0f;
                if (fromTop < round) shrink = round - Mathf.Sqrt(round * round - (round - fromTop) * (round - fromTop));
                if (fromBottom < 0.03f) shrink = Mathf.Max(shrink, 0.03f - fromBottom);
                bool isTop = i == count;
                Color color = Color.white;
                if (!isTop)
                {
                    color = Color.Lerp(WallBottom, WallTop, Mathf.SmoothStep(0f, 1f, f));
                    // Two faint strata and a lit rim just under the top edge.
                    float strata = Mathf.Max(Mathf.Clamp01(1f - Mathf.Abs(f - 0.34f) / 0.05f),
                        Mathf.Clamp01(1f - Mathf.Abs(f - 0.63f) / 0.04f));
                    color = Color.Lerp(color, Strata, strata * 0.55f);
                    float rim = Mathf.Clamp01(1f - Mathf.Abs(f - 0.9f) / 0.08f);
                    color = Color.Lerp(color, RimLight, rim * rim * 0.25f);
                }
                AddLayer(y, isTop ? 1f : 0f, isTop ? 0f : shrink, color);
            }
            ApplyMesh(bodyMesh);
        }

        private void AddLayer(float y, float kind, float shrink, Color color)
        {
            int start = vertices.Count;
            AddVertex(new Vector3(bounds.xMin * cellSize, y, bounds.yMin * cellSize), color, new Vector2(0f, 0f), kind, shrink);
            AddVertex(new Vector3(bounds.xMin * cellSize, y, bounds.yMax * cellSize), color, new Vector2(0f, 1f), kind, shrink);
            AddVertex(new Vector3(bounds.xMax * cellSize, y, bounds.yMax * cellSize), color, new Vector2(1f, 1f), kind, shrink);
            AddVertex(new Vector3(bounds.xMax * cellSize, y, bounds.yMin * cellSize), color, new Vector2(1f, 0f), kind, shrink);
            triangles.Add(start); triangles.Add(start + 1); triangles.Add(start + 2);
            triangles.Add(start); triangles.Add(start + 2); triangles.Add(start + 3);
        }

        private void AddVertex(Vector3 position, Color color, Vector2 uv, float kind, float shrink)
        {
            vertices.Add(position);
            // Unlike the sRGB top texture, vertex colors are not decoded by the GPU.
            colors.Add(QualitySettings.activeColorSpace == ColorSpace.Linear ? color.linear : color);
            uvs.Add(uv);
            layers.Add(new Vector2(kind, shrink));
        }

        private void ClearBuffers()
        {
            vertices.Clear();
            colors.Clear();
            uvs.Clear();
            layers.Clear();
            triangles.Clear();
        }

        private void ApplyMesh(Mesh mesh)
        {
            mesh.Clear();
            mesh.SetVertices(vertices);
            mesh.SetColors(colors);
            mesh.SetUVs(0, uvs);
            mesh.SetUVs(1, layers);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
        }

        // ---- Rubble: small shaded blocks that bounce once on the floor and fade ----

        private void SpawnRubble(int count, float speed, bool wholeBlock)
        {
            for (int i = 0; i < count; i++)
            {
                var position = new Vector3(Range(-half.x, half.x) * 0.9f * cellSize,
                    wholeBlock ? Range(bottom + (top - bottom) * 0.3f, top) : top,
                    Range(-half.y, half.y) * 0.9f * cellSize);
                Vector3 outward = new Vector3(position.x, 0f, position.z);
                if (outward.sqrMagnitude < 1e-4f) outward = new Vector3(Range(-1f, 1f), 0f, Range(-1f, 1f));
                outward = outward.normalized;
                float scale = Range(0.06f, wholeBlock ? 0.16f : 0.1f) * cellSize;
                double tone = random.NextDouble();
                Color color = tone < 0.3 ? FrameHigh : tone < 0.6 ? CobbleLight : tone < 0.85 ? CobbleDark : WallTop;
                chunks.Add(new Chunk
                {
                    position = position,
                    velocity = (outward * Range(0.8f, 2.4f) + Vector3.up * Range(2.2f, 4.2f)) * cellSize * speed,
                    spin = new Vector3(Range(-540f, 540f), Range(-360f, 360f), Range(-540f, 540f)),
                    rotation = Quaternion.Euler(Range(0f, 360f), Range(0f, 360f), Range(0f, 360f)),
                    size = new Vector3(scale, scale * Range(0.55f, 0.9f), scale * Range(0.7f, 1.1f)),
                    color = color,
                    lifetime = Range(0.85f, 1.25f) * (0.65f + speed * 0.35f)
                });
            }
            rubbleRenderer.enabled = true;
        }

        private void AdvanceRubble(float dt)
        {
            ClearBuffers();
            float floor = bottom;
            for (int i = chunks.Count - 1; i >= 0; i--)
            {
                Chunk chunk = chunks[i];
                chunk.age += dt;
                if (chunk.age >= chunk.lifetime) { chunks.RemoveAt(i); continue; }
                chunk.velocity += Vector3.down * (cellSize * 16f * dt);
                chunk.position += chunk.velocity * dt;
                float rest = floor + chunk.size.y * 0.5f;
                if (chunk.position.y < rest && chunk.velocity.y < 0f)
                {
                    // A dull bounce: stone loses most of its speed on the tiles.
                    chunk.position.y = rest;
                    chunk.velocity = new Vector3(chunk.velocity.x * 0.55f, -chunk.velocity.y * 0.3f, chunk.velocity.z * 0.55f);
                    chunk.spin *= 0.5f;
                }
                chunk.rotation = Quaternion.Euler(chunk.spin * dt) * chunk.rotation;
                chunks[i] = chunk;
                float life = chunk.age / chunk.lifetime;
                Color color = chunk.color;
                color.a = 1f - Mathf.Clamp01((life - 0.7f) / 0.3f);
                AddCube(chunk.position, chunk.rotation, chunk.size * (1f - Mathf.Clamp01((life - 0.75f) / 0.25f) * 0.6f), color);
            }
            ApplyMesh(rubbleMesh);
            if (chunks.Count == 0) rubbleRenderer.enabled = false;
        }

        private static readonly Vector3[] FaceNormals =
            { Vector3.up, Vector3.down, Vector3.left, Vector3.right, Vector3.forward, Vector3.back };

        // Flat-shaded box: each face takes a fixed top-left light, so rubble reads as solid stone.
        private void AddCube(Vector3 center, Quaternion rotation, Vector3 size, Color color)
        {
            Vector3 extents = size * 0.5f;
            foreach (Vector3 normal in FaceNormals)
            {
                Vector3 tangent = Mathf.Abs(normal.y) > 0.5f ? Vector3.right : Vector3.up;
                Vector3 bitangent = Vector3.Cross(normal, tangent);
                float shade = 0.58f + 0.48f * Mathf.Max(0f, Vector3.Dot(rotation * normal, Light3));
                Color faceColor = new Color(color.r * shade, color.g * shade, color.b * shade, color.a);
                int start = vertices.Count;
                for (int corner = 0; corner < 4; corner++)
                {
                    float a = corner == 0 || corner == 3 ? -1f : 1f;
                    float b = corner < 2 ? -1f : 1f;
                    Vector3 local = Vector3.Scale(normal + tangent * a + bitangent * b, extents);
                    AddVertex(center + rotation * local, faceColor, Vector2.zero, 2f, 0f);
                }
                triangles.Add(start); triangles.Add(start + 1); triangles.Add(start + 2);
                triangles.Add(start); triangles.Add(start + 2); triangles.Add(start + 3);
            }
        }

        // ---- Dust: soft camera-facing puffs that swell and thin out ----

        private void SpawnPuffs(int count, float strength, bool wholeBlock)
        {
            for (int i = 0; i < count; i++)
            {
                // Around the base for a collapse, over the top for a chip.
                float angle = Range(0f, Mathf.PI * 2f);
                var around = new Vector3(Mathf.Cos(angle) * half.x, 0f, Mathf.Sin(angle) * half.y);
                Vector3 position = wholeBlock
                    ? new Vector3(around.x * cellSize, bottom + cellSize * 0.1f, around.z * cellSize)
                    : new Vector3(Range(-half.x, half.x) * 0.8f * cellSize, top, Range(-half.y, half.y) * 0.8f * cellSize);
                Vector3 outward = new Vector3(around.x, 0f, around.z).normalized;
                puffs.Add(new Puff
                {
                    position = position,
                    velocity = (outward * Range(0.4f, 1f) + Vector3.up * Range(0.3f, 0.8f)) * cellSize * strength,
                    radius = Range(0.12f, 0.2f) * cellSize * (0.6f + strength * 0.4f),
                    growth = Range(1f, 1.6f),
                    alpha = Range(0.4f, 0.6f),
                    lifetime = Range(0.55f, 0.85f) * (0.7f + strength * 0.3f)
                });
            }
            dustRenderer.enabled = true;
        }

        private void AdvancePuffs(float dt)
        {
            ClearBuffers();
            Camera view = Camera.main;
            Vector3 right = view != null ? transform.InverseTransformDirection(view.transform.right) : Vector3.right;
            Vector3 up = view != null ? transform.InverseTransformDirection(view.transform.up) : Vector3.forward;
            const int Segments = 12;
            for (int i = puffs.Count - 1; i >= 0; i--)
            {
                Puff puff = puffs[i];
                puff.age += dt;
                if (puff.age >= puff.lifetime) { puffs.RemoveAt(i); continue; }
                puff.velocity *= Mathf.Max(0f, 1f - 3f * dt);
                puff.position += puff.velocity * dt;
                puffs[i] = puff;
                float life = puff.age / puff.lifetime;
                float radius = puff.radius * (1f + puff.growth * (1f - (1f - life) * (1f - life)));
                Color center = Dust;
                center.a = puff.alpha * Mathf.Min(1f, life * 8f) * (1f - life) * (1f - life);
                Color rim = center;
                rim.a = 0f;
                int start = vertices.Count;
                AddVertex(puff.position, center, Vector2.zero, 2f, 0f);
                for (int s = 0; s < Segments; s++)
                {
                    float a = s * Mathf.PI * 2f / Segments;
                    AddVertex(puff.position + (right * Mathf.Cos(a) + up * Mathf.Sin(a)) * radius, rim, Vector2.zero, 2f, 0f);
                    triangles.Add(start);
                    triangles.Add(start + 1 + (s + 1) % Segments);
                    triangles.Add(start + 1 + s);
                }
            }
            ApplyMesh(dustMesh);
            if (puffs.Count == 0) dustRenderer.enabled = false;
        }

        // ---- Shadow and counter ----

        private void CreateShadow(PrefabRegistry registry)
        {
            shadowPivot = new GameObject("Shadow Pivot").transform;
            shadowPivot.SetParent(transform, false);
            var outline = Rect.MinMaxRect(-half.x, -half.y, half.x, half.y);
            var mask = new SoftShadowMask(outline, 16f, registry.boxShadowSoftness);
            mask.AddRect(outline);
            shadow = SoftShadowMask.CreateRenderer(shadowPivot, registry.shadowMaterial, "Soft Shadow");
            mask.ApplyTo(shadow, registry.boxShadowHeight, registry.boxShadowOffset, cellSize);
        }

        private void CreateLabel()
        {
            if (TMP_Settings.defaultFontAsset == null) return;
            var labelObject = new GameObject("Obstacle Counter");
            labelObject.transform.SetParent(transform, false);
            labelRest = new Vector3(0f, top + cellSize * 0.02f, 0f);
            labelObject.transform.localPosition = labelRest;
            labelObject.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            label = labelObject.AddComponent<TextMeshPro>();
            MeshRenderer renderer = label.GetComponent<MeshRenderer>();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            label.font = TMP_Settings.defaultFontAsset;
            label.fontSize = 36f;
            label.fontStyle = FontStyles.Bold;
            label.isOrthographic = true;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Overflow;
            label.alignment = TextAlignmentOptions.Midline;
            label.color = Color.white;
            if (labelMaterial == null && label.fontSharedMaterial != null)
            {
                labelMaterial = new Material(label.fontSharedMaterial) { name = "Obstacle Counter" };
                if (labelMaterial.HasProperty("_OutlineWidth"))
                {
                    labelMaterial.EnableKeyword("OUTLINE_ON");
                    labelMaterial.SetFloat("_OutlineWidth", 0.3f);
                    labelMaterial.SetColor("_OutlineColor", new Color32(0x1C, 0x20, 0x2B, 0xFF));
                }
                if (labelMaterial.HasProperty("_FaceColor")) labelMaterial.SetColor("_FaceColor", Color.white);
                if (labelMaterial.HasProperty("_FaceDilate")) labelMaterial.SetFloat("_FaceDilate", 0.22f);
            }
            if (labelMaterial != null) label.fontSharedMaterial = labelMaterial;
            Vector2 preferred = label.GetPreferredValues("8");
            label.rectTransform.sizeDelta = preferred * 2f;
            label.text = "8";
            label.ForceMeshUpdate();
            // Mesh bounds include SDF padding; size the visible digit to the seal instead.
            TMP_CharacterInfo digit = label.textInfo.characterInfo[0];
            float glyphHeight = digit.textElement.glyph.metrics.height * digit.scale;
            float target = Mathf.Min(0.42f, sealRadius * 1.3f) * cellSize;
            labelScale = glyphHeight > 0f ? target / glyphHeight : cellSize * 0.01f;
            labelObject.transform.localScale = Vector3.one * labelScale;
        }

        private float Range(float min, float max) => min + (float)random.NextDouble() * (max - min);

        private void OnDestroy()
        {
            SoftShadowMask.Release(shadow);
            if (bodyMesh != null) Destroy(bodyMesh);
            if (rubbleMesh != null) Destroy(rubbleMesh);
            if (dustMesh != null) Destroy(dustMesh);
            if (texture != null) Destroy(texture);
        }
    }
}
