using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The pointing hand. Plays a looping tap on a point, or a press-drag-release along a path,
/// with a dotted trail and touch ripples. Targets are screen-pixel callbacks, re-read every
/// frame, so the hand follows things that move. All timing is unscaled.
/// </summary>
public sealed class TutorialHand : MonoBehaviour
{
    private const float PressedScale = 0.82f;
    private const float DotSpacing = 42f;
    private const float DotSize = 17f;

    public bool IsVisible => group.alpha > 0.01f || fadeTarget > 0f;
    public bool PointsDown { get; private set; }

    private TutorialConfig config;
    private TutorialOverlay overlay;
    private RectTransform tip;
    private RectTransform art;
    private CanvasGroup group;
    private RectTransform trailRoot;
    private CanvasGroup trailGroup;
    private readonly List<Image> dots = new List<Image>();
    private readonly List<Image> ripples = new List<Image>();
    private Coroutine routine;
    private float fadeTarget;
    private float trailTarget;
    private int dotCount;
    private float dotChase;
    private Vector2 flip = Vector2.one;

    internal static TutorialHand Create(TutorialConfig config, RectTransform parent)
    {
        var trail = new GameObject("Hand Trail", typeof(RectTransform)).GetComponent<RectTransform>();
        trail.SetParent(parent, false);
        TutorialOverlay.Stretch(trail);

        var go = new GameObject("Hand", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var hand = go.AddComponent<TutorialHand>();
        hand.config = config;
        hand.overlay = parent.GetComponent<TutorialOverlay>();
        hand.trailRoot = trail;
        hand.trailGroup = trail.gameObject.AddComponent<CanvasGroup>();
        hand.trailGroup.alpha = 0f;
        hand.trailGroup.blocksRaycasts = false;
        hand.Build();
        return hand;
    }

    private void Build()
    {
        tip = (RectTransform)transform;
        tip.sizeDelta = Vector2.zero;
        group = gameObject.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        group.blocksRaycasts = false;
        group.interactable = false;

        var artGo = new GameObject("Art", typeof(RectTransform));
        artGo.transform.SetParent(tip, false);
        art = (RectTransform)artGo.transform;

        // A soft copy under the hand grounds it on the screen.
        Image shadow = TutorialOverlay.CreateChild<Image>("Shadow", art);
        Image image = TutorialOverlay.CreateChild<Image>("Image", art);
        foreach (Image layer in new[] { shadow, image })
        {
            layer.sprite = config.handSprite;
            layer.preserveAspect = true;
            RectTransform rect = layer.rectTransform;
            rect.pivot = config.handFingertip;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = Vector2.one * config.handSize;
            rect.anchoredPosition = Vector2.zero;
        }
        shadow.color = new Color(0f, 0f, 0f, 0.3f);
        shadow.rectTransform.anchoredPosition = new Vector2(10f, -14f);
    }

    // ---------------- Public API ----------------

    /// <summary>Loops a tap on the given screen point.</summary>
    public void PlayTap(Func<Vector2?> screenPoint)
    {
        Restart(TapLoop(screenPoint));
    }

    /// <summary>Loops a drag along the screen-space polyline (first point is where the press lands).</summary>
    public void PlayDrag(Func<List<Vector2>> screenPath)
    {
        Restart(DragLoop(screenPath));
    }

    public void Stop()
    {
        if (routine != null) StopCoroutine(routine);
        routine = null;
        fadeTarget = 0f;
        trailTarget = 0f;
    }

    public void HideImmediate()
    {
        Stop();
        group.alpha = 0f;
        trailGroup.alpha = 0f;
        foreach (Image ripple in ripples) ripple.gameObject.SetActive(false);
    }

    private void Restart(IEnumerator loop)
    {
        Stop();
        routine = StartCoroutine(loop);
    }

    // ---------------- Loops ----------------

    private IEnumerator TapLoop(Func<Vector2?> screenPoint)
    {
        trailTarget = 0f;
        while (true)
        {
            Vector2 point;
            while (!TryLocal(screenPoint, out point)) yield return null;
            Orient(point);
            Vector2 rest = point + Away() * 90f;
            if (group.alpha <= 0.01f) SetPose(rest, 1.1f);
            fadeTarget = 1f;

            yield return Move(() => TryLocal(screenPoint, out Vector2 p) ? p : point, 0.32f, 1f);
            yield return Scale(PressedScale, 0.1f);
            if (TryLocal(screenPoint, out point)) SpawnRipple(point);
            yield return Wait(0.12f, () => Follow(screenPoint));
            yield return Scale(1f, 0.18f);
            yield return Wait(0.18f, () => Follow(screenPoint));
            yield return Move(() => (TryLocal(screenPoint, out Vector2 p) ? p : point) + Away() * 90f, 0.3f, 1f);
            yield return Wait(0.25f, null);
        }
    }

    private IEnumerator DragLoop(Func<List<Vector2>> screenPath)
    {
        while (true)
        {
            List<Vector2> path = null;
            while (path == null)
            {
                path = ToLocal(screenPath?.Invoke());
                if (path == null) yield return null;
            }
            Orient(path);
            LayTrail(path);
            trailTarget = 1f;

            // Hover in beside the box, press on it.
            SetPose(path[0] + Away() * 90f, 1.1f);
            fadeTarget = 1f;
            yield return Move(() => path[0], 0.3f, 1f);
            yield return Scale(PressedScale, 0.12f);
            SpawnRipple(path[0]);
            yield return Wait(0.12f, null);

            // Drag along the route at a steady, readable pace.
            float length = PathLength(path);
            float duration = Mathf.Clamp(length / 520f, 0.75f, 2f);
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                t = t * t * (3f - 2f * t);
                tip.anchoredPosition = PointAlong(path, t * length);
                yield return null;
            }

            yield return Wait(0.18f, null);
            yield return Scale(1f, 0.16f);
            fadeTarget = 0f;
            yield return Wait(0.45f, null);
        }
    }

    // ---------------- Frame update ----------------

    private void LateUpdate()
    {
        float dt = Time.unscaledDeltaTime;
        group.alpha = Mathf.MoveTowards(group.alpha, fadeTarget, dt * 5f);
        trailGroup.alpha = Mathf.MoveTowards(trailGroup.alpha, trailTarget, dt * 4f);

        // A bright pulse runs along the dotted trail, showing the direction to drag.
        if (trailGroup.alpha > 0f && dotCount > 0)
        {
            dotChase += dt * 9f;
            for (int i = 0; i < dotCount; i++)
            {
                float wave = Mathf.Repeat(dotChase - i, dotCount + 6f);
                float glow = wave < 4f ? 1f - Mathf.Abs(wave - 2f) / 2f : 0f;
                Color color = config.trailColor;
                color.a *= 0.45f + 0.55f * glow;
                dots[i].color = color;
                dots[i].rectTransform.localScale = Vector3.one * (1f + 0.35f * glow);
            }
        }

        for (int i = 0; i < ripples.Count; i++)
        {
            Image ripple = ripples[i];
            if (!ripple.gameObject.activeSelf) continue;
            float age = ripple.rectTransform.localScale.z + dt / 0.55f;
            if (age >= 1f)
            {
                ripple.gameObject.SetActive(false);
                continue;
            }
            float eased = 1f - (1f - age) * (1f - age);
            float size = Mathf.Lerp(0.3f, 1.5f, eased);
            ripple.rectTransform.localScale = new Vector3(size, size, age);
            ripple.color = new Color(1f, 1f, 1f, 0.9f * (1f - age));
        }
    }

    // ---------------- Pieces ----------------

    private IEnumerator Move(Func<Vector2> target, float duration, float endScale)
    {
        Vector2 start = tip.anchoredPosition;
        float startScale = art.localScale.x / Mathf.Max(0.001f, Mathf.Abs(flip.x));
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = 1f - Mathf.Pow(1f - t, 3f);
            tip.anchoredPosition = Vector2.LerpUnclamped(start, target(), eased);
            SetScale(Mathf.Lerp(startScale, endScale, eased));
            yield return null;
        }
    }

    private IEnumerator Scale(float to, float duration)
    {
        float from = art.localScale.x / Mathf.Max(0.001f, Mathf.Abs(flip.x));
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            // Release overshoots a touch, press does not.
            float eased = to > from ? EaseOutBack(t) : 1f - (1f - t) * (1f - t);
            SetScale(Mathf.LerpUnclamped(from, to, eased));
            yield return null;
        }
        SetScale(to);
    }

    private static IEnumerator Wait(float seconds, Action tick)
    {
        float elapsed = 0f;
        while (elapsed < seconds)
        {
            elapsed += Time.unscaledDeltaTime;
            tick?.Invoke();
            yield return null;
        }
    }

    private void Follow(Func<Vector2?> screenPoint)
    {
        if (TryLocal(screenPoint, out Vector2 point)) tip.anchoredPosition = point;
    }

    private void SetPose(Vector2 position, float scale)
    {
        tip.anchoredPosition = position;
        SetScale(scale);
    }

    private void SetScale(float scale) => art.localScale = new Vector3(scale * flip.x, scale * flip.y, 1f);

    private void Orient(Vector2 point) => Orient(new List<Vector2> { point });

    // The hand hangs down-right of its fingertip; mirror it when that would leave the screen
    // anywhere along the points it will visit.
    private void Orient(List<Vector2> points)
    {
        Rect bounds = overlay.LocalBounds;
        float reach = config.handSize * 0.85f;
        bool mirrorX = false, mirrorY = false;
        foreach (Vector2 point in points)
        {
            mirrorX |= point.x + reach > bounds.xMax - 20f;
            mirrorY |= point.y - reach < bounds.yMin + 20f;
        }
        PointsDown = mirrorY;
        var newFlip = new Vector2(mirrorX ? -1f : 1f, mirrorY ? -1f : 1f);
        if (newFlip == flip) return;
        float scale = art.localScale.x / Mathf.Max(0.001f, Mathf.Abs(flip.x));
        flip = newFlip;
        SetScale(scale);
    }

    // Direction from the fingertip toward the wrist, used for hovering beside the target.
    private Vector2 Away() => new Vector2(0.6f * flip.x, -0.8f * flip.y);

    private void SpawnRipple(Vector2 point)
    {
        Image ripple = null;
        foreach (Image candidate in ripples)
            if (!candidate.gameObject.activeSelf) { ripple = candidate; break; }
        if (ripple == null)
        {
            ripple = TutorialOverlay.CreateChild<Image>("Ripple", trailRoot.parent);
            ripple.sprite = TutorialSprites.Ring;
            ripple.rectTransform.sizeDelta = Vector2.one * 150f;
            ripples.Add(ripple);
        }
        // Behind the hand, above the shade.
        ripple.transform.SetSiblingIndex(tip.GetSiblingIndex());
        ripple.rectTransform.anchoredPosition = point;
        ripple.rectTransform.localScale = new Vector3(0.3f, 0.3f, 0f);
        ripple.gameObject.SetActive(true);
    }

    private void LayTrail(List<Vector2> path)
    {
        float length = PathLength(path);
        int count = Mathf.Max(0, Mathf.FloorToInt(length / DotSpacing) - 1);
        while (dots.Count < count)
        {
            Image dot = TutorialOverlay.CreateChild<Image>("Dot", trailRoot);
            dot.sprite = TutorialSprites.Circle;
            dot.rectTransform.sizeDelta = Vector2.one * DotSize;
            dots.Add(dot);
        }
        for (int i = 0; i < dots.Count; i++)
        {
            bool used = i < count;
            dots[i].gameObject.SetActive(used);
            if (used) dots[i].rectTransform.anchoredPosition = PointAlong(path, (i + 1) * DotSpacing);
        }
        dotCount = count;
        dotChase = 0f;
    }

    private bool TryLocal(Func<Vector2?> screenPoint, out Vector2 local)
    {
        Vector2? screen = screenPoint?.Invoke();
        local = screen.HasValue ? overlay.ScreenToLocal(screen.Value) : default;
        return screen.HasValue;
    }

    private List<Vector2> ToLocal(List<Vector2> screenPath)
    {
        if (screenPath == null || screenPath.Count < 2) return null;
        var local = new List<Vector2>(screenPath.Count);
        foreach (Vector2 point in screenPath) local.Add(overlay.ScreenToLocal(point));
        return local;
    }

    private static float PathLength(List<Vector2> path)
    {
        float length = 0f;
        for (int i = 1; i < path.Count; i++) length += Vector2.Distance(path[i - 1], path[i]);
        return length;
    }

    private static Vector2 PointAlong(List<Vector2> path, float distance)
    {
        for (int i = 1; i < path.Count; i++)
        {
            float segment = Vector2.Distance(path[i - 1], path[i]);
            if (distance <= segment && segment > 0f) return Vector2.Lerp(path[i - 1], path[i], distance / segment);
            distance -= segment;
        }
        return path[path.Count - 1];
    }

    private static float EaseOutBack(float t)
    {
        const float c1 = 1.70158f, c3 = c1 + 1f;
        float u = t - 1f;
        return 1f + c3 * u * u * u + c1 * u * u;
    }
}
