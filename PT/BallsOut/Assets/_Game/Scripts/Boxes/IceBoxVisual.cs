using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

namespace BallsOut
{
    // Procedural ice block over a box footprint: rounded, bevelled slab with
    // polish streaks, counter, progressive cracks and a shatter burst.
    // Pure presentation; BoxController owns the count.
    internal sealed class IceBoxVisual : MonoBehaviour
    {
        // Cracks start once the counter drops below this value, one stage per step.
        private const int CrackStartCount = 3;
        private const int MaxCrackStage = CrackStartCount - 1;
        private const float PunchDuration = 0.3f;
        private const float ShakeDuration = 0.28f;
        private const float FlashDuration = 0.09f;
        private const float ShardLifetime = 0.7f;

        // Footprint, in cells: gap to neighbouring blocks, corner radius, bevel width.
        private const float Inset = 0.035f;
        private const float CornerRadius = 0.16f;
        private const float BevelWidth = 0.11f;
        private const float DistanceRange = 0.3f;
        private const float Padding = 0.12f;
        private const int PixelsPerCell = 56;
        private const float LayerStep = 0.022f;

        private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int FlashId = Shader.PropertyToID("_Flash");
        private static readonly int RangeId = Shader.PropertyToID("_Range");

        // Palette sampled from the reference ice blocks.
        private static readonly Color TopLow = new Color32(0x4F, 0xB8, 0xEC, 0xFF);
        private static readonly Color TopHigh = new Color32(0x86, 0xD6, 0xF7, 0xFF);
        private static readonly Color RimLight = new Color32(0xD8, 0xF6, 0xFF, 0xFF);
        private static readonly Color RimShade = new Color32(0x2F, 0x8F, 0xD6, 0xFF);
        private static readonly Color WallTop = new Color32(0x45, 0xA8, 0xE6, 0xFF);
        private static readonly Color WallBottom = new Color32(0x1E, 0x6F, 0xC0, 0xFF);
        private static readonly Color CrackShade = new Color32(0x2A, 0x7F, 0xC8, 0xFF);
        private static readonly Color ShardColor = new Color32(0xA8, 0xE6, 0xFB, 0xFF);
        private static readonly Vector2 LightDirection = new Vector2(-0.35f, 1f).normalized;
        private static Material material;
        private static Material labelMaterial;

        private struct Crack
        {
            public Vector2 a, b;
            public float widthA, widthB;
            public int stage;
        }

        private struct Shard
        {
            public Vector3 position, velocity, spin, rotation;
            public Vector3 v0, v1, v2;
            public Color color;
            public float age, lifetime;
        }

        private readonly List<Vector3> vertices = new List<Vector3>();
        private readonly List<Color> colors = new List<Color>();
        private readonly List<Vector2> uvs = new List<Vector2>();
        private readonly List<Vector2> layers = new List<Vector2>();
        private readonly List<int> triangles = new List<int>();
        private readonly List<Crack> cracks = new List<Crack>();
        private readonly List<Shard> shards = new List<Shard>();
        private readonly List<Vector4> rects = new List<Vector4>();
        private readonly List<Vector2> fillets = new List<Vector2>();
        private readonly List<Vector2> filletSigns = new List<Vector2>();
        private HashSet<Vector2Int> cells;
        private IReadOnlyList<Vector2Int> cellList;
        private float cellSize;
        private float bottom;
        private float top;
        private Vector3 pivot;
        private Rect bounds;
        private Vector2 footprintMin;
        private Vector2 footprintMax;
        private Texture2D texture;
        private Color32[] pixels;
        private Transform body;
        private Mesh bodyMesh;
        private MeshRenderer bodyRenderer;
        private Mesh shardMesh;
        private MeshRenderer shardRenderer;
        private MaterialPropertyBlock properties;
        private TextMeshPro label;
        private float labelScale;
        private System.Random random;
        private int crackStage;
        private float punch = -1f;
        private float shake = -1f;
        private float breakTime = -1f;
        private bool shattered;

        internal static IceBoxVisual Create(BoxController box, float cellSize, int count)
        {
            var root = new GameObject("Ice");
            root.transform.SetParent(box.transform, false);
            var ice = root.AddComponent<IceBoxVisual>();
            ice.Build(box, cellSize, count);
            return ice;
        }

        private void Build(BoxController box, float size, int count)
        {
            cellSize = size;
            cellList = box.Shape.Cells;
            cells = new HashSet<Vector2Int>(cellList);
            random = new System.Random(StableHash(box.Id));

            // Match the height of the box art it replaces, plus a little.
            float artTop = size * 0.4f;
            foreach (Renderer renderer in box.GetComponentsInChildren<Renderer>(true))
                if (renderer.gameObject.name != "Soft Shadow")
                    artTop = Mathf.Max(artTop, box.transform.InverseTransformPoint(renderer.bounds.max).y);
            bottom = size * 0.01f;
            top = artTop + size * 0.03f;

            Vector3 centroid = Vector3.zero;
            footprintMin = Vector2.positiveInfinity;
            footprintMax = Vector2.negativeInfinity;
            foreach (Vector2Int cell in cellList)
            {
                centroid += new Vector3(cell.x, 0f, cell.y);
                footprintMin = Vector2.Min(footprintMin, cell - Vector2.one * 0.5f);
                footprintMax = Vector2.Max(footprintMax, cell + Vector2.one * 0.5f);
            }
            pivot = centroid * (size / cellList.Count);
            bounds = Rect.MinMaxRect(footprintMin.x - Padding, footprintMin.y - Padding,
                footprintMax.x + Padding, footprintMax.y + Padding);
            BuildOutline();

            if (material == null)
            {
                Shader shader = Shader.Find("Balls Out/Ice Box") ?? Shader.Find("Sprites/Default");
                material = new Material(shader) { name = "Ice Box" };
            }
            texture = new Texture2D(Mathf.CeilToInt(bounds.width * PixelsPerCell), Mathf.CeilToInt(bounds.height * PixelsPerCell),
                TextureFormat.RGBA32, false)
            {
                name = "Ice Top",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            pixels = new Color32[texture.width * texture.height];
            properties = new MaterialPropertyBlock();
            properties.SetTexture(MainTexId, texture);
            properties.SetFloat(RangeId, DistanceRange);
            properties.SetColor(ColorId, Color.white);
            properties.SetFloat(FlashId, 0f);

            body = new GameObject("Ice Body", typeof(MeshFilter), typeof(MeshRenderer)).transform;
            body.SetParent(transform, false);
            body.localPosition = pivot;
            bodyMesh = new Mesh { name = "Ice Body" };
            body.GetComponent<MeshFilter>().sharedMesh = bodyMesh;
            bodyRenderer = SetupRenderer(body.GetComponent<MeshRenderer>());
            bodyRenderer.SetPropertyBlock(properties);
            BuildBodyMesh();

            var shardObject = new GameObject("Ice Shards", typeof(MeshFilter), typeof(MeshRenderer));
            shardObject.transform.SetParent(transform, false);
            shardMesh = new Mesh { name = "Ice Shards" };
            shardMesh.MarkDynamic();
            shardObject.GetComponent<MeshFilter>().sharedMesh = shardMesh;
            shardRenderer = SetupRenderer(shardObject.GetComponent<MeshRenderer>());
            shardRenderer.enabled = false;

            for (int stage = 1; stage <= MaxCrackStage; stage++) GenerateCracks(stage);
            CreateLabel();
            crackStage = CrackStageFor(count);
            label.text = count.ToString();
            BakeTop();
        }

        private static MeshRenderer SetupRenderer(MeshRenderer renderer)
        {
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return renderer;
        }

        private static int CrackStageFor(int count) => Mathf.Clamp(CrackStartCount - count, 0, MaxCrackStage);

        internal void SetCount(int count)
        {
            if (shattered) return;
            if (count <= 0)
            {
                shattered = true;
                breakTime = 0f;
                punch = -1f;
                shake = 0f;
                return;
            }
            label.text = count.ToString();
            punch = 0f;
            shake = 0f;
            int stage = CrackStageFor(count);
            if (stage == crackStage) return;
            crackStage = stage;
            BakeTop();
            SpawnShards(3 + cellList.Count, 0.55f, 0.6f);
        }

        private void Update()
        {
            float dt = Mathf.Min(Time.deltaTime, 1f / 20f);
            if (punch >= 0f)
            {
                punch += dt;
                float k = Mathf.Clamp01(punch / PunchDuration);
                label.transform.localScale = Vector3.one * (labelScale * (1f + Mathf.Sin(k * Mathf.PI) * 0.4f));
                if (k >= 1f) punch = -1f;
            }
            if (shake >= 0f && body.gameObject.activeSelf)
            {
                shake += dt;
                float k = Mathf.Clamp01(shake / ShakeDuration);
                float offset = Mathf.Sin(shake * 70f) * cellSize * 0.035f * (1f - k);
                body.localPosition = pivot + new Vector3(offset, 0f, 0f);
                if (k >= 1f) { shake = -1f; body.localPosition = pivot; }
            }
            if (breakTime >= 0f && body.gameObject.activeSelf)
            {
                breakTime += dt;
                float k = Mathf.Clamp01(breakTime / FlashDuration);
                // A quick swell and white-out, then the block bursts into shards.
                body.localScale = Vector3.one * (1f + k * 0.07f);
                properties.SetFloat(FlashId, k * 0.85f);
                bodyRenderer.SetPropertyBlock(properties);
                label.transform.localScale = Vector3.one * (labelScale * (1f + k * 0.5f));
                if (k >= 1f)
                {
                    body.gameObject.SetActive(false);
                    label.gameObject.SetActive(false);
                    SpawnShards(Mathf.Min(10 + cellList.Count * 9, 42), 1f, 1f);
                }
            }
            if (shards.Count != 0) AdvanceShards(dt);
            else if (shattered && !body.gameObject.activeSelf) Destroy(gameObject);
        }

        // ---- Footprint signed distance (cells; negative inside) ----

        private void BuildOutline()
        {
            // Each cell reaches into its neighbours' centres so seams never read as edges.
            foreach (Vector2Int cell in cellList)
                rects.Add(new Vector4(
                    cell.x - (cells.Contains(cell + Vector2Int.left) ? 1f : 0.5f - Inset),
                    cell.y - (cells.Contains(cell + Vector2Int.down) ? 1f : 0.5f - Inset),
                    cell.x + (cells.Contains(cell + Vector2Int.right) ? 1f : 0.5f - Inset),
                    cell.y + (cells.Contains(cell + Vector2Int.up) ? 1f : 0.5f - Inset)));
            // Inner (concave) corners get a matching fillet so L shapes stay soft.
            var corners = new HashSet<Vector2Int>();
            foreach (Vector2Int cell in cellList)
                for (int dx = 0; dx <= 1; dx++)
                    for (int dy = 0; dy <= 1; dy++)
                        corners.Add(cell + new Vector2Int(dx, dy));
            foreach (Vector2Int corner in corners)
            {
                // Corner (x, y) sits between cells x-1..x and y-1..y.
                int missing = 0;
                Vector2 sign = Vector2.zero;
                for (int dx = -1; dx <= 0; dx++)
                    for (int dy = -1; dy <= 0; dy++)
                        if (!cells.Contains(corner + new Vector2Int(dx, dy)))
                        {
                            missing++;
                            sign = new Vector2(dx == 0 ? 1f : -1f, dy == 0 ? 1f : -1f);
                        }
                if (missing != 1) continue;
                // Both edges meeting here are inset away from the missing cell.
                fillets.Add(new Vector2(corner.x - 0.5f, corner.y - 0.5f) - sign * Inset);
                filletSigns.Add(sign);
            }
        }

        private float Distance(Vector2 p)
        {
            float d = float.MaxValue;
            foreach (Vector4 r in rects)
            {
                Vector2 center = new Vector2(r.x + r.z, r.y + r.w) * 0.5f;
                Vector2 half = new Vector2(r.z - r.x, r.w - r.y) * 0.5f;
                Vector2 q = new Vector2(Mathf.Abs(p.x - center.x), Mathf.Abs(p.y - center.y)) - half + Vector2.one * CornerRadius;
                float box = new Vector2(Mathf.Max(q.x, 0f), Mathf.Max(q.y, 0f)).magnitude +
                    Mathf.Min(Mathf.Max(q.x, q.y), 0f) - CornerRadius;
                d = Mathf.Min(d, box);
            }
            for (int i = 0; i < fillets.Count; i++)
            {
                Vector2 corner = fillets[i];
                Vector2 sign = filletSigns[i];
                Vector2 squareCenter = corner + sign * (CornerRadius * 0.5f);
                Vector2 q = new Vector2(Mathf.Abs(p.x - squareCenter.x), Mathf.Abs(p.y - squareCenter.y)) - Vector2.one * (CornerRadius * 0.5f);
                float square = new Vector2(Mathf.Max(q.x, 0f), Mathf.Max(q.y, 0f)).magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0f);
                float hole = CornerRadius - (p - (corner + sign * CornerRadius)).magnitude;
                d = Mathf.Min(d, Mathf.Max(square, hole));
            }
            return d;
        }

        // ---- Baked top face: colour in RGB, signed distance in alpha ----

        private void BakeTop()
        {
            int width = texture.width;
            int height = texture.height;
            const float e = 0.01f;
            float span = Mathf.Max(0.01f, footprintMax.y - footprintMin.y);
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    var p = new Vector2(bounds.xMin + (x + 0.5f) / width * bounds.width,
                        bounds.yMin + (y + 0.5f) / height * bounds.height);
                    float d = Distance(p);
                    float depth = -d;

                    // Body: cool gradient, lighter toward the top of the screen, with soft frost.
                    float v = Mathf.Clamp01((p.y - footprintMin.y) / span);
                    Color color = Color.Lerp(TopLow, TopHigh, Mathf.SmoothStep(0f, 1f, v * 0.85f + 0.1f));
                    float frost = Mathf.PerlinNoise(p.x * 4.3f + 11f, p.y * 4.3f + 7f) * 0.6f +
                                  Mathf.PerlinNoise(p.x * 11f + 3f, p.y * 11f + 19f) * 0.4f;
                    color += new Color(1f, 1f, 1f, 0f) * ((frost - 0.5f) * 0.07f);

                    // Polish: short diagonal glints, a wide soft one next to a thin sharp one.
                    float along = p.x * 0.8f + p.y;
                    float across = (p.x - p.y * 0.8f) * 2.2f;
                    float band = Frac(along / 0.95f);
                    float wide = 1f - Mathf.Clamp01(Mathf.Abs(band - 0.32f) / 0.07f);
                    float thin = 1f - Mathf.Clamp01(Mathf.Abs(band - 0.45f) / 0.022f);
                    float segments = Mathf.Clamp01((Mathf.PerlinNoise(across + 5f, Mathf.Floor(along / 0.95f) * 3.1f) - 0.42f) * 5f);
                    float inner = Mathf.Clamp01((depth - BevelWidth) / 0.06f);
                    float glint = (wide * wide * 0.5f + thin * 0.65f) * segments * inner;
                    color = Color.Lerp(color, Color.white, glint);

                    // Bevel: rim lit from the upper left, shaded toward the lower right.
                    Vector2 normal = new Vector2(Distance(p + new Vector2(e, 0f)) - Distance(p - new Vector2(e, 0f)),
                        Distance(p + new Vector2(0f, e)) - Distance(p - new Vector2(0f, e)));
                    normal = normal.sqrMagnitude > 1e-8f ? normal.normalized : Vector2.up;
                    float lit = Vector2.Dot(normal, LightDirection);
                    Color rim = lit >= 0f ? Color.Lerp(color, RimLight, 0.35f + lit * 0.6f) : Color.Lerp(color, RimShade, -lit * 0.7f);
                    float bevel = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(depth / BevelWidth));
                    color = Color.Lerp(rim, color, bevel);
                    // Thin bright seam where the bevel meets the face.
                    float seam = 1f - Mathf.Clamp01(Mathf.Abs(depth - BevelWidth) / 0.018f);
                    color = Color.Lerp(color, RimLight, seam * 0.35f);

                    // Cracks: a darker groove under a bright fracture line.
                    if (crackStage > 0)
                    {
                        float shade = 0f, core = 0f;
                        var shaded = p + new Vector2(-0.008f, -0.012f);
                        foreach (Crack crack in cracks)
                        {
                            if (crack.stage > crackStage) continue;
                            SegmentDistance(p, crack, out float distance, out float crackWidth);
                            core = Mathf.Max(core, 1f - Mathf.Clamp01((distance - crackWidth * 0.35f) / (crackWidth * 0.5f)));
                            SegmentDistance(shaded, crack, out distance, out crackWidth);
                            shade = Mathf.Max(shade, 1f - Mathf.Clamp01((distance - crackWidth * 0.8f) / (crackWidth * 0.6f)));
                        }
                        float mask = Mathf.Clamp01(depth / 0.05f);
                        color = Color.Lerp(color, CrackShade, shade * 0.55f * mask);
                        color = Color.Lerp(color, Color.white, core * 0.95f * mask);
                    }

                    color.a = Mathf.Clamp01(0.5f - d / (2f * DistanceRange));
                    pixels[y * width + x] = color;
                }
            texture.SetPixels32(pixels);
            texture.Apply(false);
        }

        private static void SegmentDistance(Vector2 p, Crack crack, out float distance, out float width)
        {
            Vector2 ab = crack.b - crack.a;
            float t = Mathf.Clamp01(Vector2.Dot(p - crack.a, ab) / Mathf.Max(ab.sqrMagnitude, 1e-6f));
            distance = (p - (crack.a + ab * t)).magnitude;
            width = Mathf.Lerp(crack.widthA, crack.widthB, t);
        }

        private static float Frac(float value) => value - Mathf.Floor(value);

        // ---- Slab mesh: stacked outline layers form soft rounded walls ----

        private void BuildBodyMesh()
        {
            ClearBuffers();
            float height = top - bottom;
            int count = Mathf.Max(4, Mathf.CeilToInt(height / (cellSize * LayerStep)));
            float round = Mathf.Min(0.07f, height / cellSize * 0.3f);
            // Bottom to top: with ZWrite on, each layer covers the one below.
            for (int i = 0; i <= count; i++)
            {
                float y = Mathf.Lerp(bottom, top, (float)i / count);
                float fromTop = (top - y) / cellSize;
                float fromBottom = (y - bottom) / cellSize;
                float shrink = 0f;
                if (fromTop < round) shrink = round - Mathf.Sqrt(round * round - (round - fromTop) * (round - fromTop));
                if (fromBottom < 0.03f) shrink = Mathf.Max(shrink, 0.03f - fromBottom);
                bool isTop = i == count;
                Color color = isTop ? Color.white : Color.Lerp(WallBottom, WallTop, (float)i / count);
                AddLayer(y, isTop ? 1f : 0f, isTop ? 0f : shrink, color);
            }
            for (int i = 0; i < vertices.Count; i++) vertices[i] -= pivot;
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
            colors.Add(color);
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

        // ---- Shards ----

        private void SpawnShards(int count, float speed, float scale)
        {
            for (int i = 0; i < count; i++)
            {
                Vector2Int cell = cellList[random.Next(cellList.Count)];
                var position = new Vector3((cell.x + Range(-0.4f, 0.4f)) * cellSize, top,
                    (cell.y + Range(-0.4f, 0.4f)) * cellSize);
                Vector3 outward = position - pivot;
                outward.y = 0f;
                if (outward.sqrMagnitude < 1e-4f) outward = new Vector3(Range(-1f, 1f), 0f, Range(-1f, 1f));
                outward = outward.normalized;
                float radius = Range(0.07f, 0.14f) * cellSize * scale;
                float angle = Range(0f, Mathf.PI * 2f);
                double tone = random.NextDouble();
                shards.Add(new Shard
                {
                    position = position,
                    velocity = (outward * Range(1.2f, 2.8f) + Vector3.up * Range(2.2f, 4f)) * cellSize * speed,
                    spin = new Vector3(Range(-720f, 720f), Range(-540f, 540f), Range(-720f, 720f)),
                    rotation = new Vector3(Range(0f, 360f), Range(0f, 360f), 0f),
                    v0 = Corner(angle, radius),
                    v1 = Corner(angle + Range(1.6f, 2.4f), radius * Range(0.6f, 1f)),
                    v2 = Corner(angle + Range(3.6f, 4.6f), radius * Range(0.5f, 0.9f)),
                    color = tone < 0.3 ? Color.white : tone < 0.7 ? ShardColor : TopHigh,
                    lifetime = ShardLifetime * Range(0.75f, 1.1f) * (0.6f + speed * 0.4f)
                });
            }
            shardRenderer.enabled = true;
        }

        private static Vector3 Corner(float angle, float radius) => new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;

        private void AdvanceShards(float dt)
        {
            ClearBuffers();
            for (int i = shards.Count - 1; i >= 0; i--)
            {
                Shard shard = shards[i];
                shard.age += dt;
                if (shard.age >= shard.lifetime) { shards.RemoveAt(i); continue; }
                shard.velocity += Vector3.down * (cellSize * 14f * dt);
                shard.position += shard.velocity * dt;
                shard.rotation += shard.spin * dt;
                shards[i] = shard;
                float life = shard.age / shard.lifetime;
                Color color = shard.color;
                color.a *= 1f - Mathf.Clamp01((life - 0.55f) / 0.45f);
                Quaternion rotation = Quaternion.Euler(shard.rotation);
                float shrink = 1f - life * 0.35f;
                int start = vertices.Count;
                AddVertex(shard.position + rotation * shard.v0 * shrink, color, Vector2.zero, 2f, 0f);
                AddVertex(shard.position + rotation * shard.v1 * shrink, color, Vector2.zero, 2f, 0f);
                AddVertex(shard.position + rotation * shard.v2 * shrink, color * new Color(0.8f, 0.92f, 1f, 1f), Vector2.zero, 2f, 0f);
                triangles.Add(start);
                triangles.Add(start + 1);
                triangles.Add(start + 2);
            }
            ApplyMesh(shardMesh);
            if (shards.Count == 0) shardRenderer.enabled = false;
        }

        // ---- Cracks (cell units) ----

        // Each stage adds a few branching fractures radiating from impact points.
        private void GenerateCracks(int stage)
        {
            int impacts = cellList.Count + (stage == MaxCrackStage ? 1 : 0);
            for (int i = 0; i < impacts; i++)
            {
                Vector2Int cell = cellList[random.Next(cellList.Count)];
                var origin = new Vector2(cell.x + Range(-0.25f, 0.25f), cell.y + Range(-0.25f, 0.25f));
                int rays = random.Next(2, 4);
                float baseAngle = Range(0f, Mathf.PI * 2f);
                for (int r = 0; r < rays; r++)
                {
                    float angle = baseAngle + r * Mathf.PI * 2f / rays + Range(-0.4f, 0.4f);
                    GrowCrack(origin, angle, random.Next(3, 6), 0.034f, stage, true);
                }
            }
        }

        private void GrowCrack(Vector2 point, float angle, int segments, float width, int stage, bool canBranch)
        {
            for (int s = 0; s < segments; s++)
            {
                angle += Range(-0.55f, 0.55f);
                Vector2 next = point + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * Range(0.09f, 0.15f);
                if (Distance(next) > -BevelWidth * 0.8f) return;
                float nextWidth = Mathf.Max(width * 0.72f, 0.009f);
                cracks.Add(new Crack { a = point, b = next, widthA = width, widthB = nextWidth, stage = stage });
                if (canBranch && s == 1 && random.NextDouble() < 0.6)
                    GrowCrack(next, angle + (random.NextDouble() < 0.5 ? -0.9f : 0.9f), 2, nextWidth, stage, false);
                point = next;
                width = nextWidth;
            }
        }

        // ---- Counter ----

        private void CreateLabel()
        {
            // Center on the footprint when that point is on the ice (1x2, 2x2), else on the nearest cell.
            Vector2 center = new Vector2(pivot.x, pivot.z) / cellSize;
            Vector2Int nearest = cellList[0];
            float best = float.MaxValue;
            bool inside = false;
            foreach (Vector2Int cell in cellList)
            {
                Vector2 delta = center - cell;
                if (Mathf.Abs(delta.x) <= 0.5f && Mathf.Abs(delta.y) <= 0.5f) inside = true;
                if (delta.sqrMagnitude < best) { best = delta.sqrMagnitude; nearest = cell; }
            }
            if (!inside) center = nearest;

            var labelObject = new GameObject("Ice Counter");
            labelObject.transform.SetParent(transform, false);
            labelObject.transform.localPosition = new Vector3(center.x * cellSize, top + cellSize * 0.02f, center.y * cellSize);
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
            label.alignment = TextAlignmentOptions.Center;
            label.color = new Color32(0x1D, 0x1F, 0x4A, 0xFF);
            if (labelMaterial == null && label.fontSharedMaterial != null)
            {
                labelMaterial = new Material(label.fontSharedMaterial) { name = "Ice Counter" };
                if (labelMaterial.HasProperty("_OutlineWidth"))
                {
                    labelMaterial.EnableKeyword("OUTLINE_ON");
                    labelMaterial.SetFloat("_OutlineWidth", 0.24f);
                    labelMaterial.SetColor("_OutlineColor", Color.white);
                }
            }
            if (labelMaterial != null) label.fontSharedMaterial = labelMaterial;
            Vector2 preferred = label.GetPreferredValues("8");
            label.rectTransform.sizeDelta = preferred * 2f;
            labelScale = preferred.y > 0f ? cellSize * 0.42f / preferred.y : cellSize * 0.01f;
            labelObject.transform.localScale = Vector3.one * labelScale;
        }

        private float Range(float min, float max) => min + (float)random.NextDouble() * (max - min);

        private static int StableHash(string value)
        {
            unchecked
            {
                int hash = 23;
                if (value != null) foreach (char c in value) hash = hash * 31 + c;
                return hash;
            }
        }

        private void OnDestroy()
        {
            if (bodyMesh != null) Destroy(bodyMesh);
            if (shardMesh != null) Destroy(shardMesh);
            if (texture != null) Destroy(texture);
        }
    }
}
