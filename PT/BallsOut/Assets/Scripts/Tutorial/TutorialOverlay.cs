using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>A spotlight cut into the tutorial shade. Rect is in screen pixels; null keeps the last one.</summary>
public sealed class TutorialHole
{
    public readonly Func<Rect?> Rect;
    public readonly bool Circle;
    public readonly float Padding;

    public TutorialHole(Func<Rect?> rect, bool circle = false, float padding = -1f)
    {
        Rect = rect;
        Circle = circle;
        Padding = padding;
    }
}

/// <summary>
/// Top-most tutorial layer: darkens the screen with spotlight holes, shows the hand and a
/// speech bubble, and filters input so only the spotlit spots can be touched.
/// Built entirely from code; one instance lives under the tutorial director.
/// </summary>
public sealed class TutorialOverlay : MonoBehaviour
{
    private const int MaxHoles = 4;
    private const float HoleSharpness = 11f;
    private const float FadeSpeed = 4.5f;

    private static readonly int ResolutionId = Shader.PropertyToID("_Resolution");
    private static readonly int DimColorId = Shader.PropertyToID("_DimColor");
    private static readonly int HoleCountId = Shader.PropertyToID("_HoleCount");
    private static readonly int HoleRadiusId = Shader.PropertyToID("_HoleRadius");
    private static readonly int RingColorId = Shader.PropertyToID("_RingColor");
    private static readonly int RingStrengthId = Shader.PropertyToID("_RingStrength");
    private static readonly int RipplePhaseId = Shader.PropertyToID("_RipplePhase");
    private static readonly int SoftnessId = Shader.PropertyToID("_Softness");
    private static readonly int RingWidthId = Shader.PropertyToID("_RingWidth");
    private static readonly int[] HoleIds =
    {
        Shader.PropertyToID("_Hole0"), Shader.PropertyToID("_Hole1"),
        Shader.PropertyToID("_Hole2"), Shader.PropertyToID("_Hole3")
    };

    /// <summary>Tap on the shade (outside any hole that lets touches through).</summary>
    public event Action OnTapped;

    /// <summary>When true, touches inside the holes reach the UI underneath.</summary>
    public bool PassThroughHoles { get; set; }

    public TutorialHand Hand { get; private set; }
    public bool IsShown => shown;

    private TutorialConfig config;
    private Canvas canvas;
    private RectTransform root;
    private Image shade;
    private Material shadeMaterial;

    private readonly List<TutorialHole> holes = new List<TutorialHole>();
    private readonly Rect[] targetRects = new Rect[MaxHoles];
    private readonly Rect[] shownRects = new Rect[MaxHoles];
    private readonly bool[] hasRect = new bool[MaxHoles];
    private float visibility;
    private bool shown;
    private float dimMultiplier = 1f;
    private float ripple;

    private RectTransform bubble;
    private CanvasGroup bubbleGroup;
    private Image bubbleIcon;
    private TMP_Text bubbleText;
    private float bubbleHeight;
    private bool bubbleVisible;
    private float bubblePop;
    private bool bubblePlaced;
    private TMP_Text continueLabel;
    private bool continueVisible;
    private float continueAlpha;

    public static TutorialOverlay Create(TutorialConfig config, Transform parent)
    {
        var go = new GameObject("Tutorial Overlay", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var overlay = go.AddComponent<TutorialOverlay>();
        overlay.Build(config);
        return overlay;
    }

    private void Build(TutorialConfig cfg)
    {
        config = cfg;
        gameObject.layer = LayerMask.NameToLayer("UI") >= 0 ? LayerMask.NameToLayer("UI") : 5;
        canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 1000;
        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = config.referenceResolution;
        scaler.matchWidthOrHeight = config.matchWidthOrHeight;
        gameObject.AddComponent<GraphicRaycaster>();
        root = (RectTransform)transform;

        // The shade: one full-screen image whose shader cuts the holes.
        shade = CreateChild<Image>("Shade", root);
        Stretch(shade.rectTransform);
        shade.raycastTarget = true;
        shade.maskable = false;
        Shader shader = config.focusMaskShader != null ? config.focusMaskShader : Shader.Find("UI/TutorialFocusMask");
        shadeMaterial = new Material(shader) { name = "Tutorial Shade", hideFlags = HideFlags.DontSave };
        shade.material = shadeMaterial;
        // Relays raycast filtering and taps from the shade to this overlay.
        shade.gameObject.AddComponent<TutorialShadeInput>().Owner = this;

        Hand = TutorialHand.Create(config, root);
        BuildBubble();
        BuildContinueLabel();

        canvas.enabled = false;
    }

    private void BuildBubble()
    {
        bubble = new GameObject("Bubble", typeof(RectTransform)).GetComponent<RectTransform>();
        bubble.SetParent(root, false);
        bubbleGroup = bubble.gameObject.AddComponent<CanvasGroup>();
        bubbleGroup.blocksRaycasts = false;
        bubbleGroup.interactable = false;

        Image shadow = CreateChild<Image>("Shadow", bubble);
        shadow.sprite = TutorialSprites.RoundedRect;
        shadow.type = Image.Type.Sliced;
        shadow.color = new Color(0f, 0f, 0f, 0.28f);
        Stretch(shadow.rectTransform);
        shadow.rectTransform.offsetMin = new Vector2(4f, -14f);
        shadow.rectTransform.offsetMax = new Vector2(4f, -14f);

        Image panel = CreateChild<Image>("Panel", bubble);
        panel.sprite = TutorialSprites.RoundedRect;
        panel.type = Image.Type.Sliced;
        panel.color = config.bubbleColor;
        Stretch(panel.rectTransform);

        bubbleIcon = CreateChild<Image>("Icon", bubble);
        bubbleIcon.preserveAspect = true;
        RectTransform iconRect = bubbleIcon.rectTransform;
        iconRect.anchorMin = iconRect.anchorMax = new Vector2(0.5f, 1f);
        iconRect.pivot = new Vector2(0.5f, 0.5f);
        iconRect.sizeDelta = new Vector2(170f, 170f);
        iconRect.anchoredPosition = new Vector2(0f, 0f);

        bubbleText = CreateChild<TextMeshProUGUI>("Text", bubble);
        if (config.font != null) bubbleText.font = config.font;
        bubbleText.fontSize = config.bubbleFontSize;
        bubbleText.color = config.bubbleTextColor;
        bubbleText.alignment = TextAlignmentOptions.Center;
        bubbleText.textWrappingMode = TextWrappingModes.Normal;
        bubbleText.richText = true;
        bubbleText.lineSpacing = -4f;
        RectTransform textRect = bubbleText.rectTransform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;

        bubbleGroup.alpha = 0f;
        bubble.gameObject.SetActive(false);
    }

    private void BuildContinueLabel()
    {
        continueLabel = CreateChild<TextMeshProUGUI>("Continue", root);
        if (config.font != null) continueLabel.font = config.font;
        continueLabel.text = config.tapToContinue;
        continueLabel.fontSize = config.bubbleFontSize * 0.72f;
        continueLabel.color = Color.white;
        continueLabel.alignment = TextAlignmentOptions.Center;
        continueLabel.fontStyle = FontStyles.Bold;
        continueLabel.textWrappingMode = TextWrappingModes.NoWrap;
        continueLabel.rectTransform.sizeDelta = new Vector2(config.bubbleWidth, 90f);
        continueLabel.alpha = 0f;
    }

    // ---------------- Public API ----------------

    public void Show()
    {
        if (shown) return;
        shown = true;
        canvas.enabled = true;
        if (visibility <= 0f) ResetHoles();
        transform.SetAsLastSibling();
    }

    /// <summary>Fades everything out; immediate skips the fade (level torn down).</summary>
    public void Hide(bool immediate = false)
    {
        shown = false;
        SetText(null);
        ShowContinue(false);
        Hand.Stop();
        PassThroughHoles = false;
        if (!immediate) return;
        visibility = 0f;
        holes.Clear();
        ResetHoles();
        bubbleVisible = false;
        bubbleGroup.alpha = 0f;
        bubble.gameObject.SetActive(false);
        continueAlpha = 0f;
        continueLabel.alpha = 0f;
        Hand.HideImmediate();
        canvas.enabled = false;
    }

    /// <summary>0 hides the shade (another system already dims), 1 is the configured darkness.</summary>
    public void SetDim(float multiplier) => dimMultiplier = Mathf.Clamp01(multiplier);

    public void SetHoles(params TutorialHole[] newHoles)
    {
        int previous = holes.Count;
        holes.Clear();
        if (newHoles != null)
            foreach (TutorialHole hole in newHoles)
                if (hole != null && holes.Count < MaxHoles) holes.Add(hole);
        for (int i = 0; i < MaxHoles; i++)
        {
            // A hole that was not there before opens from a huge spotlight down to its target.
            if (i >= previous || !hasRect[i]) hasRect[i] = false;
        }
        UpdateTargets(true);
        bubblePlaced = false;
    }

    public void SetText(string text, Sprite icon = null)
    {
        if (string.IsNullOrEmpty(text))
        {
            bubbleVisible = false;
            return;
        }
        bubbleVisible = true;
        bubble.gameObject.SetActive(true);
        bubbleText.text = config.Format(text);
        bubbleIcon.sprite = icon;
        bubbleIcon.gameObject.SetActive(icon != null);
        LayoutBubble();
        bubblePop = 0f;
        bubblePlaced = false;
    }

    public void ShowContinue(bool visible) => continueVisible = visible;

    /// <summary>Screen-space point to overlay-local point.</summary>
    public Vector2 ScreenToLocal(Vector2 screen)
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(root, screen, null, out Vector2 local);
        return local;
    }

    public Rect LocalBounds => root.rect;

    // ---------------- Input ----------------


    internal bool BlocksAt(Vector2 screenPoint)
    {
        if (!shown) return false;
        if (!PassThroughHoles) return true;
        for (int i = 0; i < holes.Count; i++)
            if (hasRect[i] && Padded(i, targetRects[i]).Contains(screenPoint)) return false;
        return true;
    }

    internal void HandleTap()
    {
        if (shown) OnTapped?.Invoke();
    }

    // ---------------- Frame update ----------------

    private void LateUpdate()
    {
        float dt = Time.unscaledDeltaTime;
        visibility = Mathf.MoveTowards(visibility, shown ? 1f : 0f, dt * FadeSpeed);
        if (!shown && visibility <= 0f && !bubbleVisible && bubbleGroup.alpha <= 0f && !Hand.IsVisible)
        {
            if (canvas.enabled) canvas.enabled = false;
            return;
        }

        UpdateTargets(false);
        float lerp = 1f - Mathf.Exp(-HoleSharpness * dt);
        float screenSize = Mathf.Max(Screen.width, Screen.height);
        for (int i = 0; i < holes.Count; i++)
        {
            if (!hasRect[i]) continue;
            Rect target = Padded(i, targetRects[i]);
            if (shownRects[i].width <= 0f)
            {
                // Start as a screen-filling spotlight and close in on the target.
                shownRects[i] = Inflate(target, screenSize);
            }
            shownRects[i] = LerpRect(shownRects[i], target, lerp);
        }
        for (int i = holes.Count; i < MaxHoles; i++) shownRects[i] = default;

        UpdateMaterial(dt);
        UpdateBubble(dt);
        UpdateContinue(dt);
    }

    private void ResetHoles()
    {
        for (int i = 0; i < MaxHoles; i++)
        {
            hasRect[i] = false;
            shownRects[i] = default;
        }
        UpdateTargets(true);
    }

    private void UpdateTargets(bool reset)
    {
        for (int i = 0; i < holes.Count; i++)
        {
            Rect? rect = holes[i].Rect?.Invoke();
            if (rect.HasValue && rect.Value.width > 0f && rect.Value.height > 0f)
            {
                Rect value = rect.Value;
                if (holes[i].Circle)
                {
                    float size = Mathf.Max(value.width, value.height);
                    value = new Rect(value.center - Vector2.one * size * 0.5f, Vector2.one * size);
                }
                targetRects[i] = value;
                if (!hasRect[i] && reset) shownRects[i] = default;
                hasRect[i] = true;
            }
            else if (reset)
            {
                hasRect[i] = false;
                shownRects[i] = default;
            }
        }
    }

    private void UpdateMaterial(float dt)
    {
        ripple = Mathf.Repeat(ripple + dt / 1.4f, 1f);
        float pixelScale = canvas.scaleFactor;
        shadeMaterial.SetVector(ResolutionId, new Vector4(Screen.width, Screen.height, 0f, 0f));
        Color dim = config.dimColor;
        dim.a *= visibility * dimMultiplier;
        shadeMaterial.SetColor(DimColorId, dim);
        shadeMaterial.SetColor(RingColorId, config.ringColor);
        shadeMaterial.SetFloat(RingStrengthId, visibility);
        shadeMaterial.SetFloat(RipplePhaseId, ripple);
        shadeMaterial.SetFloat(SoftnessId, 9f * pixelScale);
        shadeMaterial.SetFloat(RingWidthId, 5f * pixelScale);
        int count = 0;
        var radii = Vector4.zero;
        for (int i = 0; i < holes.Count; i++)
        {
            if (!hasRect[i] || shownRects[i].width <= 0f) continue;
            Rect r = shownRects[i];
            shadeMaterial.SetVector(HoleIds[count], new Vector4(r.center.x, r.center.y, r.width * 0.5f, r.height * 0.5f));
            radii[count] = holes[i].Circle ? Mathf.Min(r.width, r.height) * 0.5f : 30f * pixelScale;
            count++;
        }
        shadeMaterial.SetVector(HoleRadiusId, radii);
        shadeMaterial.SetFloat(HoleCountId, count);
        // Unity UI caches material state per batch; nudge it so property changes always show.
        shade.SetMaterialDirty();
    }

    // ---------------- Bubble ----------------

    private void LayoutBubble()
    {
        const float padX = 56f, padY = 44f, iconSpace = 150f;
        float width = config.bubbleWidth;
        bool hasIcon = bubbleIcon.gameObject.activeSelf;
        Vector2 preferred = bubbleText.GetPreferredValues(bubbleText.text, width - padX * 2f, 0f);
        bubbleHeight = preferred.y + padY * 2f + (hasIcon ? iconSpace : 0f);
        bubble.sizeDelta = new Vector2(width, bubbleHeight);
        RectTransform textRect = bubbleText.rectTransform;
        textRect.offsetMin = new Vector2(padX, padY);
        textRect.offsetMax = new Vector2(-padX, -padY - (hasIcon ? iconSpace : 0f));
        // The icon sits on the bubble's top edge, half outside, like a badge.
        bubbleIcon.rectTransform.anchoredPosition = new Vector2(0f, -padY - iconSpace * 0.35f);
    }

    private void UpdateBubble(float dt)
    {
        float targetAlpha = bubbleVisible && shown ? 1f : 0f;
        bubbleGroup.alpha = Mathf.MoveTowards(bubbleGroup.alpha, targetAlpha, dt * 6f);
        if (bubbleGroup.alpha <= 0f && !bubbleVisible)
        {
            if (bubble.gameObject.activeSelf) bubble.gameObject.SetActive(false);
            return;
        }

        Vector2 target = BubbleTarget();
        if (!bubblePlaced)
        {
            bubble.anchoredPosition = target;
            bubblePlaced = true;
        }
        else
        {
            bubble.anchoredPosition = Vector2.Lerp(bubble.anchoredPosition, target, 1f - Mathf.Exp(-10f * dt));
        }

        // Pop in with a soft overshoot.
        bubblePop = Mathf.Min(1f, bubblePop + dt / 0.38f);
        float scale = bubbleVisible ? EaseOutBack(bubblePop) : Mathf.Lerp(0.9f, 1f, bubbleGroup.alpha);
        bubble.localScale = Vector3.one * Mathf.LerpUnclamped(0.6f, 1f, scale);
    }

    private Vector2 BubbleTarget()
    {
        Rect bounds = root.rect;
        Rect safe = SafeLocalRect();
        const float margin = 40f, gap = 70f;
        float halfW = bubble.sizeDelta.x * 0.5f;
        float halfH = bubbleHeight * 0.5f;
        float continueSpace = continueVisible ? 110f : 0f;

        if (!TryHoleUnion(out Rect union))
            return new Vector2(0f, bounds.height * 0.08f);

        float handSpace = Hand.IsVisible ? config.handSize * (Hand.PointsDown ? 0.1f : 0.75f) : 0f;
        float spaceAbove = safe.yMax - union.yMax;
        float spaceBelow = union.yMin - safe.yMin - handSpace;
        float needed = bubbleHeight + gap + margin + continueSpace;
        bool above = spaceAbove >= needed || spaceAbove >= spaceBelow;
        float y = above
            ? union.yMax + gap + halfH + (Hand.IsVisible && Hand.PointsDown ? config.handSize * 0.7f : 0f)
            : union.yMin - gap - halfH - handSpace;
        y = Mathf.Clamp(y, safe.yMin + margin + halfH + continueSpace, safe.yMax - margin - halfH);
        float x = Mathf.Clamp(union.center.x, safe.xMin + margin + halfW, safe.xMax - margin - halfW);
        return new Vector2(x, y);
    }

    private void UpdateContinue(float dt)
    {
        continueAlpha = Mathf.MoveTowards(continueAlpha, continueVisible && shown ? 1f : 0f, dt * 4f);
        float pulse = 0.6f + 0.4f * Mathf.Sin(Time.unscaledTime * 4f);
        continueLabel.alpha = continueAlpha * pulse * Mathf.Max(bubbleGroup.alpha, bubbleVisible ? 0f : 1f);
        if (continueAlpha <= 0f) return;
        Vector2 anchor = bubbleVisible
            ? bubble.anchoredPosition - new Vector2(0f, bubbleHeight * 0.5f + 70f)
            : new Vector2(0f, root.rect.yMin + root.rect.height * 0.15f);
        continueLabel.rectTransform.anchoredPosition = anchor;
    }

    // ---------------- Helpers ----------------

    private bool TryHoleUnion(out Rect union)
    {
        union = default;
        bool any = false;
        for (int i = 0; i < holes.Count; i++)
        {
            if (!hasRect[i]) continue;
            Rect padded = Padded(i, targetRects[i]);
            Vector2 min = ScreenToLocal(padded.min);
            Vector2 max = ScreenToLocal(padded.max);
            Rect local = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
            union = any ? Rect.MinMaxRect(Mathf.Min(union.xMin, local.xMin), Mathf.Min(union.yMin, local.yMin),
                Mathf.Max(union.xMax, local.xMax), Mathf.Max(union.yMax, local.yMax)) : local;
            any = true;
        }
        return any;
    }

    private Rect SafeLocalRect()
    {
        Rect safe = Screen.safeArea;
        Vector2 min = ScreenToLocal(safe.min);
        Vector2 max = ScreenToLocal(safe.max);
        return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
    }

    private Rect Padded(int index, Rect rect)
    {
        float padding = (holes[index].Padding >= 0f ? holes[index].Padding : config.holePadding) * canvas.scaleFactor;
        return Inflate(rect, padding);
    }

    private static Rect Inflate(Rect rect, float amount) =>
        Rect.MinMaxRect(rect.xMin - amount, rect.yMin - amount, rect.xMax + amount, rect.yMax + amount);

    private static Rect LerpRect(Rect a, Rect b, float t) =>
        Rect.MinMaxRect(Mathf.Lerp(a.xMin, b.xMin, t), Mathf.Lerp(a.yMin, b.yMin, t),
            Mathf.Lerp(a.xMax, b.xMax, t), Mathf.Lerp(a.yMax, b.yMax, t));

    private static float EaseOutBack(float t)
    {
        const float c1 = 1.70158f, c3 = c1 + 1f;
        float u = t - 1f;
        return 1f + c3 * u * u * u + c1 * u * u;
    }

    internal static T CreateChild<T>(string name, Transform parent) where T : Graphic
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = parent.gameObject.layer;
        go.transform.SetParent(parent, false);
        var graphic = go.AddComponent<T>();
        graphic.raycastTarget = false;
        return graphic;
    }

    internal static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private void OnDestroy()
    {
        if (shadeMaterial != null) Destroy(shadeMaterial);
    }
}

/// <summary>Sits on the shade image: lets touches through the holes and reports taps elsewhere.</summary>
internal sealed class TutorialShadeInput : MonoBehaviour, ICanvasRaycastFilter, IPointerClickHandler
{
    public TutorialOverlay Owner;

    public bool IsRaycastLocationValid(Vector2 screenPoint, Camera eventCamera) =>
        Owner != null && Owner.BlocksAt(screenPoint);

    public void OnPointerClick(PointerEventData eventData) => Owner?.HandleTap();
}
