using UnityEngine;
using UnityEngine.UI;

namespace Voodoo.Tiny.Sauce.Internal.Debugger.Widgets
{
    internal static class TSDebugBlueButtonStyle
    {
        private const int PillTextureWidth = 128;
        private const int PillTextureHeight = 48;
        private const int PillRadius = 24;
        private const int RoundedSquareTextureSize = 64;
        private const int RoundedSquareRadius = 8;
        private const int FieldTextureWidth = 64;
        private const int FieldTextureHeight = 40;
        private const int FieldRadius = 8;
        private const int RoundedSpriteAntiAliasSamples = 4;
        private const string ToggleOnResourcePath = "ToggleOn";
        private const string ToggleOffResourcePath = "ToggleOff";

        public static readonly Color PrimaryColor = new Color32(66, 133, 244, 255);
        public static readonly Color PrimaryHighlightedColor = new Color32(85, 147, 245, 255);
        public static readonly Color PrimaryPressedColor = new Color32(53, 106, 195, 255);
        public static readonly Color SecondaryButtonColor = new Color(0.85882354f, 0.87058824f, 0.8980392f);
        public static readonly Color SecondaryButtonPressedColor = new Color(0.74f, 0.76f, 0.8f);
        public static readonly Color DisabledColor = new Color(0.78f, 0.78f, 0.78f, 0.55f);
        public static readonly Color ScreenBackgroundColor = new Color32(226, 232, 242, 255);
        public static readonly Color PanelColor = Color.white;
        public static readonly Color CardColor = new Color32(230, 238, 249, 255);
        public static readonly Color FieldColor = Color.white;
        public static readonly Color DarkTextColor = new Color(0.19607843f, 0.19607843f, 0.19607843f);
        public static readonly Color MutedTextColor = new Color(0.55f, 0.55f, 0.55f);
        public static readonly Color LightTextColor = Color.white;

        private static Sprite _pillSprite;
        private static Sprite _roundedSquareSprite;
        private static Sprite _fieldSprite;
        private static Sprite _toggleOnSprite;
        private static Sprite _toggleOffSprite;

        public static void Apply(Image image, Button button)
        {
            ApplyPrimaryButton(image, button);
        }

        public static void ApplyPrimaryButton(Image image, Button button)
        {
            ApplyRoundedImage(image, GetPillSprite(), Color.white);
            ApplyButtonColors(button, PrimaryColor, PrimaryHighlightedColor, PrimaryPressedColor, DisabledColor);
        }

        public static void ApplyPrimaryFieldButton(Image image, Button button)
        {
            ApplyRoundedImage(image, GetFieldSprite(), Color.white);
            ApplyButtonColors(button, PrimaryColor, PrimaryHighlightedColor, PrimaryPressedColor, DisabledColor);
        }

        public static void ApplySecondaryButton(Image image, Button button)
        {
            ApplyRoundedImage(image, GetPillSprite(), Color.white);
            ApplyButtonColors(button, SecondaryButtonColor, Color.white, SecondaryButtonPressedColor, DisabledColor);
        }

        public static void ApplySurface(Image image, Color color)
        {
            ApplyRoundedImage(image, GetRoundedSquareSprite(), color);
        }

        public static void ApplyPillSurface(Image image, Color color)
        {
            ApplyRoundedImage(image, GetPillSprite(), color);
        }

        public static void ApplyToggle(Image image, bool isOn)
        {
            if (image == null)
                return;

            image.sprite = isOn ? GetToggleOnSprite() : GetToggleOffSprite();
            image.type = Image.Type.Simple;
            image.color = Color.white;
            image.preserveAspect = true;
        }

        public static void ApplyField(Image image)
        {
            ApplyRoundedImage(image, GetFieldSprite(), FieldColor);
        }

        public static void ApplyAccentFill(Image image)
        {
            ApplyRoundedImage(image, GetFieldSprite(), PrimaryColor);
        }

        private static void ApplyRoundedImage(Image image, Sprite sprite, Color color)
        {
            if (image == null)
                return;

            image.sprite = sprite;
            image.type = Image.Type.Sliced;
            image.color = color;
            image.preserveAspect = false;
        }

        private static void ApplyButtonColors(Button button, Color normal, Color highlighted, Color pressed, Color disabled)
        {
            if (button == null)
                return;

            button.transition = Selectable.Transition.ColorTint;
            button.colors = new ColorBlock
            {
                normalColor = normal,
                highlightedColor = highlighted,
                pressedColor = pressed,
                selectedColor = highlighted,
                disabledColor = disabled,
                colorMultiplier = 1f,
                fadeDuration = 0.1f
            };
        }

        private static Sprite GetPillSprite()
        {
            if (_pillSprite == null)
                _pillSprite = CreateRoundedSprite(PillTextureWidth, PillTextureHeight, PillRadius);

            return _pillSprite;
        }

        private static Sprite GetRoundedSquareSprite()
        {
            if (_roundedSquareSprite == null)
                _roundedSquareSprite = CreateRoundedSprite(RoundedSquareTextureSize, RoundedSquareTextureSize, RoundedSquareRadius);

            return _roundedSquareSprite;
        }

        private static Sprite GetFieldSprite()
        {
            if (_fieldSprite == null)
                _fieldSprite = CreateRoundedSprite(FieldTextureWidth, FieldTextureHeight, FieldRadius);

            return _fieldSprite;
        }

        private static Sprite GetToggleOnSprite()
        {
            if (_toggleOnSprite == null)
                _toggleOnSprite = Resources.Load<Sprite>(ToggleOnResourcePath);

            return _toggleOnSprite;
        }

        private static Sprite GetToggleOffSprite()
        {
            if (_toggleOffSprite == null)
                _toggleOffSprite = Resources.Load<Sprite>(ToggleOffResourcePath);

            return _toggleOffSprite;
        }

        private static Sprite CreateRoundedSprite(int width, int height, int radius)
        {
            var effectiveRadius = Mathf.Min(radius, width * 0.5f, height * 0.5f);
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = "TSDebugRoundedSprite",
                hideFlags = HideFlags.HideAndDontSave,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

            var pixels = new Color[width * height];

            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                    pixels[y * width + x] = GetRoundedRectanglePixelColor(x, y, width, height, effectiveRadius);
            }

            texture.SetPixels(pixels);
            texture.Apply(false, true);

            var border = new Vector4(effectiveRadius, effectiveRadius, effectiveRadius, effectiveRadius);
            return Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
        }

        private static Color GetRoundedRectanglePixelColor(int x, int y, int width, int height, float radius)
        {
            var coverage = GetRoundedRectangleCoverage(x, y, width, height, radius);
            return new Color(1f, 1f, 1f, coverage);
        }

        private static float GetRoundedRectangleCoverage(int x, int y, int width, int height, float radius)
        {
            var coveredSamples = 0;
            var totalSamples = RoundedSpriteAntiAliasSamples * RoundedSpriteAntiAliasSamples;

            for (var sampleY = 0; sampleY < RoundedSpriteAntiAliasSamples; sampleY++)
            {
                for (var sampleX = 0; sampleX < RoundedSpriteAntiAliasSamples; sampleX++)
                {
                    var sampledX = x + (sampleX + 0.5f) / RoundedSpriteAntiAliasSamples;
                    var sampledY = y + (sampleY + 0.5f) / RoundedSpriteAntiAliasSamples;

                    if (IsInsideRoundedRectangle(sampledX, sampledY, width, height, radius))
                        coveredSamples++;
                }
            }

            return coveredSamples / (float)totalSamples;
        }

        private static bool IsInsideRoundedRectangle(float x, float y, int width, int height, float radius)
        {
            var innerX = Mathf.Clamp(x, radius, width - radius);
            var innerY = Mathf.Clamp(y, radius, height - radius);
            var distanceX = x - innerX;
            var distanceY = y - innerY;
            return distanceX * distanceX + distanceY * distanceY <= radius * radius;
        }
    }
}
