using UnityEngine;

/// <summary>
/// Small anti-aliased sprites the tutorial draws with, generated once at runtime so the
/// tutorial needs no extra art beyond the hand.
/// </summary>
internal static class TutorialSprites
{
    private static Sprite circle;
    private static Sprite ring;
    private static Sprite roundedRect;

    public static Sprite Circle => circle != null ? circle : circle = Create(64, 32f, 0f, false, "Tutorial Circle");
    public static Sprite Ring => ring != null ? ring : ring = Create(128, 64f, 7f, false, "Tutorial Ring");
    // 9-sliced: stretches to any bubble size while keeping 32px corners.
    public static Sprite RoundedRect => roundedRect != null ? roundedRect : roundedRect = Create(96, 34f, 0f, true, "Tutorial Rounded");

    private static Sprite Create(int size, float radius, float ringWidth, bool rounded, string name)
    {
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = name,
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            hideFlags = HideFlags.DontSave
        };
        var pixels = new Color32[size * size];
        float half = size * 0.5f;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                var p = new Vector2(x + 0.5f - half, y + 0.5f - half);
                float d;
                if (rounded)
                {
                    // Rounded square filling the texture, radius-wide corners.
                    Vector2 q = new Vector2(Mathf.Abs(p.x), Mathf.Abs(p.y)) - new Vector2(half - radius, half - radius);
                    d = Vector2.Max(q, Vector2.zero).magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0f) - radius;
                }
                else
                {
                    d = p.magnitude - (radius - 1f);
                    if (ringWidth > 0f) d = Mathf.Abs(d + ringWidth * 0.5f) - ringWidth * 0.5f;
                }
                byte a = (byte)Mathf.RoundToInt(Mathf.Clamp01(0.5f - d) * 255f);
                pixels[y * size + x] = new Color32(255, 255, 255, a);
            }
        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        Vector4 border = rounded ? new Vector4(radius, radius, radius, radius) : Vector4.zero;
        Sprite sprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
            SpriteMeshType.FullRect, border);
        sprite.name = name;
        sprite.hideFlags = HideFlags.DontSave;
        return sprite;
    }
}
