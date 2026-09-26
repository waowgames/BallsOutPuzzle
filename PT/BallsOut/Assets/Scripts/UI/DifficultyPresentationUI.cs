using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Hard / Very Hard level presentation: the intro banner (red edges + star badge + title) on the
/// first attempt, the difficulty badge under the timer, and a pulsing edge glow on Very Hard levels.
/// Purely visual; the level itself is untouched. The clock and board input wait for the intro.
/// Lives on the intro root, which stays active and hides through its CanvasGroup.
/// </summary>
public sealed class DifficultyPresentationUI : MonoBehaviour, IPointerClickHandler
{
    [Serializable]
    private struct Style
    {
        public string title;
        public Sprite star;
        public Sprite badgeFrame;
        public Color vignetteColor;
        public Color raysColor;
        public Material titleMaterial;
        public Color titleTop;
        public Color titleBottom;
        [Tooltip("Shakes the banner and double-pulses the red edges.")]
        public bool intense;
        public AudioClip introSound;
    }

    [Header("Intro")]
    [SerializeField] private CanvasGroup introGroup;
    [SerializeField] private Image introDim;
    [SerializeField] private Image introVignette;
    [SerializeField] private RectTransform introContent;
    [SerializeField] private RectTransform introBadge;
    [SerializeField] private Image introStar;
    [SerializeField] private Image introRays;
    [SerializeField] private RectTransform introIcon;
    [SerializeField] private TMP_Text introTitle;
    [SerializeField] private CanvasGroup introTitleGroup;

    [Header("HUD Badge")]
    [SerializeField] private CanvasGroup hudBadgeGroup;
    [SerializeField] private RectTransform hudBadge;
    [SerializeField] private Image hudBadgeFrame;
    [SerializeField] private RectTransform hudBadgeIcon;
    [SerializeField] private Image hudBadgeRays;

    [Header("Very Hard Edge Glow")]
    [SerializeField] private CanvasGroup ambientGlow;
    [SerializeField, Range(0f, 1f)] private float ambientMinAlpha = 0.15f;
    [SerializeField, Range(0f, 1f)] private float ambientMaxAlpha = 0.5f;
    [SerializeField, Min(0.05f)] private float ambientBeat = 0.55f;

    [Header("Styles")]
    [SerializeField] private Style hard = new Style
    {
        title = "HARD\nLEVEL",
        vignetteColor = new Color(1f, 0.12f, 0.12f, 0.95f),
        raysColor = new Color(1f, 0.9f, 0.55f, 1f),
        titleTop = Color.white,
        titleBottom = new Color(1f, 0.82f, 0.86f),
    };
    [SerializeField] private Style veryHard = new Style
    {
        title = "VERY HARD\nLEVEL",
        vignetteColor = new Color(1f, 0.05f, 0.02f, 1f),
        raysColor = new Color(1f, 0.35f, 0.2f, 1f),
        titleTop = new Color(1f, 0.93f, 0.45f),
        titleBottom = new Color(1f, 0.45f, 0.1f),
        intense = true,
    };

    [Header("Timing")]
    [SerializeField, Min(0f)] private float holdDuration = 1.2f;
    [SerializeField, Min(0.05f)] private float flyDuration = 0.5f;

    private const float DimAlpha = 0.45f;
    private const float RaysAlpha = 0.55f;

    private LevelDifficulty current;
    private Sequence introSequence;
    private float flyStartTime;
    private bool introPlaying;
    private bool badgeShown;
    private Vector2 introBadgeHome;
    private Tween introRaysSpin;
    private Tween badgeRaysSpin;
    private Tween badgePulse;
    private Tween ambientTween;

    private void Awake()
    {
        if (introBadge != null)
            introBadgeHome = introBadge.anchoredPosition;

        HideIntroImmediate();
        SetAmbient(false);
        SetBadgeShown(false, false);
    }

    private void OnEnable()
    {
        GameEvents.OnLevelLoaded += HandleLevelLoaded;
        GameEvents.OnLevelStarted += HandleLevelStarted;

        if (LevelManager.Instance != null)
        {
            current = LevelManager.Instance.CurrentDifficulty;
            ApplyTheme();
        }
    }

    private void OnDisable()
    {
        GameEvents.OnLevelLoaded -= HandleLevelLoaded;
        GameEvents.OnLevelStarted -= HandleLevelStarted;

        StopIntro();
        SetAmbient(false);
        SetBadgeShown(false, false);
    }

    private void Update()
    {
        if (hudBadgeGroup == null || !badgeShown)
            return;

        // The freeze bar sits right under the timer, so step aside while it is up.
        bool frozen = LevelTimer.Instance != null && LevelTimer.Instance.IsFrozen;
        hudBadgeGroup.alpha = Mathf.MoveTowards(hudBadgeGroup.alpha, frozen ? 0f : 1f, Time.deltaTime * 6f);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        // Tap skips the hold and sends the badge straight to the HUD.
        if (introPlaying && introSequence != null && introSequence.Elapsed() < flyStartTime)
            introSequence.Goto(flyStartTime, true);
    }

    private void HandleLevelLoaded(int _)
    {
        StopIntro();
        current = LevelManager.Instance != null
            ? LevelManager.Instance.CurrentDifficulty
            : LevelDifficulty.Normal;
        ApplyTheme();
    }

    private void HandleLevelStarted(int _)
    {
        LevelManager manager = LevelManager.Instance;
        if (current != LevelDifficulty.Normal && manager != null && manager.CurrentAttempt <= 1)
            PlayIntro();
    }

    private Style CurrentStyle => current == LevelDifficulty.VeryHard ? veryHard : hard;

    private void ApplyTheme()
    {
        bool themed = current != LevelDifficulty.Normal;
        if (themed && hudBadgeFrame != null && CurrentStyle.badgeFrame != null)
            hudBadgeFrame.sprite = CurrentStyle.badgeFrame;

        SetBadgeShown(themed, false);
        SetAmbient(current == LevelDifficulty.VeryHard);
    }

    // ------------------------------------------------------------------ Intro

    private void PlayIntro()
    {
        if (introGroup == null || introBadge == null)
            return;

        StopIntro();
        Style style = CurrentStyle;
        introPlaying = true;
        UIManager.Instance?.SetGameplayBlocked(this, true);
        SetBadgeShown(false, false);
        SetAmbient(false);

        if (style.introSound != null)
            SoundManager.Instance?.PlaySfx(style.introSound);

        // Reset every piece to its pre-intro state.
        introGroup.gameObject.SetActive(true);
        introGroup.alpha = 0f;
        introGroup.blocksRaycasts = true;
        if (introDim != null) SetAlpha(introDim, DimAlpha);
        if (introVignette != null)
        {
            Color vignette = style.vignetteColor;
            vignette.a = 0f;
            introVignette.color = vignette;
        }
        if (introStar != null)
        {
            if (style.star != null) introStar.sprite = style.star;
            SetAlpha(introStar, 1f);
        }
        if (introRays != null)
        {
            introRays.color = WithAlpha(style.raysColor, 0f);
            introRays.rectTransform.localRotation = Quaternion.identity;
            introRaysSpin = introRays.rectTransform
                .DORotate(new Vector3(0f, 0f, -360f), 7f, RotateMode.FastBeyond360)
                .SetEase(Ease.Linear).SetLoops(-1);
        }
        if (introTitle != null)
        {
            introTitle.text = style.title;
            if (style.titleMaterial != null) introTitle.fontSharedMaterial = style.titleMaterial;
            introTitle.enableVertexGradient = true;
            introTitle.colorGradient = new VertexGradient(style.titleTop, style.titleTop, style.titleBottom, style.titleBottom);
            introTitle.transform.localScale = Vector3.one * 1.6f;
        }
        if (introTitleGroup != null) introTitleGroup.alpha = 0f;
        if (introContent != null) introContent.anchoredPosition = Vector2.zero;
        introBadge.anchoredPosition = introBadgeHome;
        introBadge.localScale = Vector3.zero;

        float settle = 0.55f;
        flyStartTime = settle + holdDuration + (style.intense ? 0.35f : 0f);

        Sequence seq = DOTween.Sequence();
        seq.Insert(0f, introGroup.DOFade(1f, 0.2f));
        if (introVignette != null)
            seq.Insert(0f, introVignette.DOFade(style.vignetteColor.a, 0.3f).SetEase(Ease.OutQuad));
        seq.Insert(0.1f, introBadge.DOScale(1f, 0.5f).SetEase(Ease.OutBack));
        if (introRays != null)
            seq.Insert(0.15f, introRays.DOFade(RaysAlpha, 0.4f));
        if (introTitle != null)
            seq.Insert(0.35f, introTitle.transform.DOScale(1f, 0.35f).SetEase(Ease.OutBack));
        if (introTitleGroup != null)
            seq.Insert(0.35f, introTitleGroup.DOFade(1f, 0.2f));

        if (style.intense)
        {
            // Very Hard: the banner slams in, shakes and the edges throb like a heartbeat.
            if (introContent != null)
                seq.Insert(settle, introContent.DOShakeAnchorPos(0.55f, 22f, 28, 90f, false, true));
            if (introVignette != null)
                seq.Insert(settle, introVignette.DOFade(style.vignetteColor.a * 0.45f, 0.16f)
                    .SetEase(Ease.InOutSine).SetLoops(6, LoopType.Yoyo));
            seq.Insert(settle + 0.6f, introBadge.DOPunchScale(Vector3.one * 0.14f, 0.4f, 7, 0.5f));
        }
        else
        {
            seq.Insert(settle + 0.25f, introBadge.DOPunchScale(Vector3.one * 0.08f, 0.35f, 6, 0.5f));
        }

        AppendFlyToHud(seq, flyStartTime);
        seq.OnComplete(FinishIntro);
        introSequence = seq;
    }

    private void AppendFlyToHud(Sequence seq, float at)
    {
        Vector3 targetPosition = introBadge.position;
        float targetScale = 0.25f;

        if (hudBadgeIcon != null && introIcon != null && introBadge.parent != null)
        {
            // Land the skull exactly on the HUD badge's skull.
            float introIconWorld = introIcon.rect.width * introIcon.localScale.x * introBadge.parent.lossyScale.x;
            float hudIconWorld = hudBadgeIcon.rect.width * hudBadgeIcon.lossyScale.x;
            targetScale = introIconWorld > 0f ? hudIconWorld / introIconWorld : targetScale;
            Vector3 iconOffset = introBadge.parent.TransformVector(introIcon.localPosition) * targetScale;
            targetPosition = hudBadgeIcon.position - iconOffset;
        }

        if (introTitleGroup != null)
            seq.Insert(at, introTitleGroup.DOFade(0f, 0.2f));
        if (introTitle != null)
            seq.Insert(at, introTitle.transform.DOScale(0.6f, 0.25f).SetEase(Ease.InBack));
        if (introRays != null)
            seq.Insert(at, introRays.DOFade(0f, 0.2f));
        if (introStar != null)
            seq.Insert(at, introStar.DOFade(0f, flyDuration * 0.6f));
        if (introDim != null)
            seq.Insert(at + 0.05f, introDim.DOFade(0f, flyDuration));
        if (introVignette != null)
            seq.Insert(at + 0.05f, introVignette.DOFade(0f, flyDuration));

        seq.Insert(at, introBadge.DOMove(targetPosition, flyDuration).SetEase(Ease.InOutCubic));
        seq.Insert(at, introBadge.DOScale(targetScale, flyDuration).SetEase(Ease.InOutCubic));
    }

    private void FinishIntro()
    {
        introSequence = null;
        HideIntroImmediate();
        SetBadgeShown(true, true);
        SetAmbient(current == LevelDifficulty.VeryHard);
    }

    private void StopIntro()
    {
        if (introSequence != null)
        {
            introSequence.Kill();
            introSequence = null;
        }

        if (introPlaying)
            HideIntroImmediate();
    }

    private void HideIntroImmediate()
    {
        introPlaying = false;
        UIManager.Instance?.SetGameplayBlocked(this, false);

        introRaysSpin?.Kill();
        introRaysSpin = null;

        if (introGroup != null)
        {
            introGroup.alpha = 0f;
            introGroup.blocksRaycasts = false;
            introGroup.interactable = false;
        }
        if (introContent != null)
            introContent.anchoredPosition = Vector2.zero;
        if (introBadge != null)
        {
            introBadge.anchoredPosition = introBadgeHome;
            introBadge.localScale = Vector3.zero;
        }
    }

    // ------------------------------------------------------------------ HUD badge

    private void SetBadgeShown(bool shown, bool animate)
    {
        badgeShown = shown;
        badgePulse?.Kill();
        badgeRaysSpin?.Kill();
        badgePulse = badgeRaysSpin = null;

        if (hudBadge == null)
            return;

        hudBadge.DOKill();
        hudBadge.gameObject.SetActive(shown);
        if (hudBadgeGroup != null)
            hudBadgeGroup.alpha = shown ? 1f : 0f;

        if (!shown)
            return;

        bool veryHardLevel = current == LevelDifficulty.VeryHard;
        if (hudBadgeRays != null)
        {
            hudBadgeRays.gameObject.SetActive(veryHardLevel);
            if (veryHardLevel)
                badgeRaysSpin = hudBadgeRays.rectTransform
                    .DORotate(new Vector3(0f, 0f, -360f), 5f, RotateMode.FastBeyond360)
                    .SetEase(Ease.Linear).SetLoops(-1);
        }

        if (animate)
        {
            hudBadge.localScale = Vector3.one * 0.4f;
            badgePulse = hudBadge.DOScale(1f, 0.35f).SetEase(Ease.OutBack)
                .OnComplete(() => badgePulse = veryHardLevel ? StartBadgePulse() : null);
        }
        else
        {
            hudBadge.localScale = Vector3.one;
            badgePulse = veryHardLevel ? StartBadgePulse() : null;
        }
    }

    private Tween StartBadgePulse()
    {
        return hudBadge.DOScale(1.1f, 0.45f).SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo);
    }

    // ------------------------------------------------------------------ Very Hard edge glow

    private void SetAmbient(bool on)
    {
        ambientTween?.Kill();
        ambientTween = null;

        if (ambientGlow == null)
            return;

        ambientGlow.blocksRaycasts = false;
        ambientGlow.gameObject.SetActive(on);
        ambientGlow.alpha = ambientMinAlpha;
        if (!on)
            return;

        // Heartbeat: two quick throbs, then a rest.
        ambientTween = DOTween.Sequence()
            .Append(ambientGlow.DOFade(ambientMaxAlpha, ambientBeat * 0.25f).SetEase(Ease.OutQuad))
            .Append(ambientGlow.DOFade(ambientMinAlpha + (ambientMaxAlpha - ambientMinAlpha) * 0.35f, ambientBeat * 0.25f))
            .Append(ambientGlow.DOFade(ambientMaxAlpha * 0.85f, ambientBeat * 0.2f).SetEase(Ease.OutQuad))
            .Append(ambientGlow.DOFade(ambientMinAlpha, ambientBeat * 0.8f).SetEase(Ease.InOutSine))
            .AppendInterval(ambientBeat * 0.6f)
            .SetLoops(-1);
    }

    private static void SetAlpha(Graphic graphic, float alpha)
    {
        graphic.color = WithAlpha(graphic.color, alpha);
    }

    private static Color WithAlpha(Color color, float alpha)
    {
        color.a = alpha;
        return color;
    }
}
