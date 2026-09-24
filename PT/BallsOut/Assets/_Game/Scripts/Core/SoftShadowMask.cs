using UnityEngine;
using UnityEngine.Rendering;

namespace BallsOut
{
    // Fake drop shadow: a silhouette rasterized once in local XZ, blurred on the CPU and
    // shown on one transparent quad. No shadow maps, lights or per-frame work.
    internal sealed class SoftShadowMask
    {
        private const int BlurPasses = 3;
        private static readonly int MaskId = Shader.PropertyToID("_MainTex");
        private static Mesh quad;
        // Four coverage bits per pixel (2x2 supersampling), so overlapping parts union cleanly.
        private readonly byte[] coverage;
        private readonly int columns, rows, radius;
        private readonly float pixelsPerUnit;
        private readonly Vector2 origin;

        internal SoftShadowMask(Rect bounds, float pixelsPerUnit, float softness)
        {
            this.pixelsPerUnit = pixelsPerUnit;
            // Three box passes of radius r approximate a Gaussian with sigma = sqrt(r(r + 1)).
            float sigma = softness * pixelsPerUnit;
            radius = Mathf.Max(1, Mathf.RoundToInt(Mathf.Sqrt(sigma * sigma + 0.25f) - 0.5f));
            int pad = radius * BlurPasses + 1;
            origin = bounds.min - Vector2.one * (pad / pixelsPerUnit);
            columns = Mathf.CeilToInt(bounds.width * pixelsPerUnit) + pad * 2;
            rows = Mathf.CeilToInt(bounds.height * pixelsPerUnit) + pad * 2;
            coverage = new byte[columns * rows];
        }

        internal void AddRect(Rect rect)
        {
            AddTriangle(rect.min, new Vector2(rect.xMax, rect.yMin), rect.max);
            AddTriangle(rect.min, rect.max, new Vector2(rect.xMin, rect.yMax));
        }

        internal void AddTriangle(Vector2 a, Vector2 b, Vector2 c)
        {
            a = (a - origin) * pixelsPerUnit;
            b = (b - origin) * pixelsPerUnit;
            c = (c - origin) * pixelsPerUnit;
            float area = Cross(b - a, c - a);
            if (Mathf.Abs(area) < 1e-6f) return;
            if (area < 0f) (b, c) = (c, b);
            int minX = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.x, Mathf.Min(b.x, c.x))));
            int maxX = Mathf.Min(columns - 1, Mathf.FloorToInt(Mathf.Max(a.x, Mathf.Max(b.x, c.x))));
            int minY = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.y, Mathf.Min(b.y, c.y))));
            int maxY = Mathf.Min(rows - 1, Mathf.FloorToInt(Mathf.Max(a.y, Mathf.Max(b.y, c.y))));
            for (int y = minY; y <= maxY; y++)
                for (int x = minX; x <= maxX; x++)
                {
                    int bits = 0;
                    for (int sample = 0; sample < 4; sample++)
                    {
                        var p = new Vector2(x + 0.25f + 0.5f * (sample & 1), y + 0.25f + 0.5f * (sample >> 1));
                        if (Cross(b - a, p - a) >= 0f && Cross(c - b, p - b) >= 0f && Cross(a - c, p - c) >= 0f)
                            bits |= 1 << sample;
                    }
                    coverage[y * columns + x] |= (byte)bits;
                }
        }

        // Bakes the blurred mask onto `renderer`'s quad, replacing any mask it showed before.
        // `offset` is in mask units; `scale` converts mask units to the parent's local units.
        internal void ApplyTo(MeshRenderer renderer, float height, Vector2 offset, float scale)
        {
            var pixels = new float[coverage.Length];
            for (int i = 0; i < coverage.Length; i++)
            {
                int bits = coverage[i];
                pixels[i] = ((bits & 1) + (bits >> 1 & 1) + (bits >> 2 & 1) + (bits >> 3 & 1)) * 0.25f;
            }
            var scratch = new float[pixels.Length];
            for (int pass = 0; pass < BlurPasses; pass++)
            {
                Blur(pixels, scratch, columns, rows, 1, columns);
                Blur(scratch, pixels, rows, columns, columns, 1);
            }
            var bytes = new byte[pixels.Length];
            for (int i = 0; i < pixels.Length; i++) bytes[i] = (byte)Mathf.Clamp(Mathf.RoundToInt(pixels[i] * 255f), 0, 255);
            var texture = new Texture2D(columns, rows, TextureFormat.Alpha8, false)
            {
                name = "Soft Shadow Mask",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.DontSave
            };
            texture.SetPixelData(bytes, 0);
            texture.Apply(false, true);

            Release(renderer);
            var block = new MaterialPropertyBlock();
            block.SetTexture(MaskId, texture);
            renderer.SetPropertyBlock(block);
            Transform shadow = renderer.transform;
            shadow.localPosition = new Vector3(origin.x + offset.x, 0f, origin.y + offset.y) * scale + Vector3.up * height;
            shadow.localRotation = Quaternion.identity;
            shadow.localScale = new Vector3(columns / pixelsPerUnit * scale, 1f, rows / pixelsPerUnit * scale);
        }

        internal static MeshRenderer CreateRenderer(Transform parent, Material material, string name)
        {
            var shadow = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer)) { hideFlags = HideFlags.DontSave };
            shadow.transform.SetParent(parent, false);
            shadow.GetComponent<MeshFilter>().sharedMesh = Quad;
            var renderer = shadow.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return renderer;
        }

        // The mask lives only in the property block, so it is found there even after a domain reload.
        internal static void Release(Renderer renderer)
        {
            if (renderer == null || !renderer.HasPropertyBlock()) return;
            var block = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block);
            Texture texture = block.GetTexture(MaskId);
            if (texture == null) return;
            if (Application.isPlaying) Object.Destroy(texture);
            else Object.DestroyImmediate(texture);
            renderer.SetPropertyBlock(null);
        }

        private static Mesh Quad
        {
            get
            {
                if (quad != null) return quad;
                quad = new Mesh { name = "Soft Shadow Quad", hideFlags = HideFlags.DontSave };
                quad.SetVertices(new[] { Vector3.zero, Vector3.forward, new Vector3(1f, 0f, 1f), Vector3.right });
                quad.SetUVs(0, new[] { Vector2.zero, Vector2.up, Vector2.one, Vector2.right });
                quad.SetNormals(new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up });
                quad.SetTriangles(new[] { 0, 1, 2, 0, 2, 3 }, 0);
                return quad;
            }
        }

        // One sliding-window box pass; the padded border keeps every window inside zeros.
        private void Blur(float[] source, float[] target, int length, int lines, int step, int lineStep)
        {
            float scale = 1f / (radius * 2 + 1);
            for (int line = 0; line < lines; line++)
            {
                int start = line * lineStep;
                float sum = 0f;
                for (int i = 0; i < radius && i < length; i++) sum += source[start + i * step];
                for (int i = 0; i < length; i++)
                {
                    if (i + radius < length) sum += source[start + (i + radius) * step];
                    if (i - radius - 1 >= 0) sum -= source[start + (i - radius - 1) * step];
                    target[start + i * step] = sum * scale;
                }
            }
        }

        private static float Cross(Vector2 u, Vector2 v) => u.x * v.y - u.y * v.x;
    }
}
