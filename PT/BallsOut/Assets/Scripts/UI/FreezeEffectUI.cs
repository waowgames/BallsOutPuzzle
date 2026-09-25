using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Time-freeze booster feedback: frosts the screen edges, tints the timer and shows a
/// countdown bar while <see cref="LevelTimer"/> is frozen.
/// </summary>
public sealed class FreezeEffectUI : MonoBehaviour
{
    [Header("Frost Overlay")]
    [SerializeField] private CanvasGroup overlay;
    [SerializeField] private RectTransform overlayRect;
    [SerializeField] private RectTransform[] snowflakes = System.Array.Empty<RectTransform>();

    [Header("Countdown Bar")]
    [SerializeField] private CanvasGroup bar;
    [SerializeField] private RectTransform barFill;
    [SerializeField] private TMP_Text secondsText;

    [Header("Timer")]
    [SerializeField] private Image timerBackground;
    [SerializeField] private Color frozenTimerTint = new Color(0.62f, 0.86f, 1f);

    [Header("Animation")]
    [SerializeField, Min(0.01f)] private float fadeInDuration = 0.35f;
    [SerializeField, Min(0.01f)] private float fadeOutDuration = 0.45f;
    [SerializeField, Min(0f)] private float snowfallSpeed = 70f;

    private const float SnowfallDistance = 260f;

    private bool shown;
    private int shownSeconds = -1;
    private Color timerNormalColor = Color.white;
    private Vector2[] snowflakeOrigins;
    private float[] snowflakePhases;
    private Graphic[] snowflakeGraphics;

    private void Awake()
    {
        if (timerBackground != null)
            timerNormalColor = timerBackground.color;

        snowflakeOrigins = new Vector2[snowflakes.Length];
        snowflakePhases = new float[snowflakes.Length];
        snowflakeGraphics = new Graphic[snowflakes.Length];
        for (int i = 0; i < snowflakes.Length; i++)
        {
            if (snowflakes[i] == null)
                continue;

            snowflakeOrigins[i] = snowflakes[i].anchoredPosition;
            snowflakeGraphics[i] = snowflakes[i].GetComponent<Graphic>();
            snowflakePhases[i] = i * 1.7f;
        }

        SetHiddenImmediate();
    }

    private void OnDisable()
    {
        KillTweens();
        SetHiddenImmediate();
    }

    private void Update()
    {
        LevelTimer timer = LevelTimer.Instance;
        bool frozen = timer != null && timer.HasTimeLimit && timer.IsFrozen;

        if (frozen != shown)
        {
            if (frozen) Show();
            else Hide();
        }

        if (!shown)
            return;

        RefreshBar(timer);
        AnimateSnowflakes();
    }

    private void Show()
    {
        shown = true;
        shownSeconds = -1;
        KillTweens();

        if (overlay != null)
        {
            overlay.gameObject.SetActive(true);
            overlay.DOFade(1f, fadeInDuration).SetEase(Ease.OutQuad);
        }

        if (overlayRect != null)
        {
            overlayRect.localScale = Vector3.one * 1.12f;
            overlayRect.DOScale(1f, fadeInDuration * 1.4f).SetEase(Ease.OutCubic);
        }

        if (bar != null)
        {
            bar.gameObject.SetActive(true);
            bar.DOFade(1f, fadeInDuration);
            bar.transform.localScale = Vector3.one * 0.4f;
            bar.transform.DOScale(1f, fadeInDuration).SetEase(Ease.OutBack);
        }

        if (timerBackground != null)
            timerBackground.DOColor(frozenTimerTint, fadeInDuration);
    }

    private void Hide()
    {
        shown = false;
        KillTweens();

        if (overlay != null)
            overlay.DOFade(0f, fadeOutDuration).SetEase(Ease.InQuad)
                .OnComplete(() => overlay.gameObject.SetActive(false));

        if (overlayRect != null)
            overlayRect.DOScale(1.08f, fadeOutDuration).SetEase(Ease.InQuad);

        if (bar != null)
        {
            bar.DOFade(0f, fadeOutDuration * 0.6f);
            bar.transform.DOScale(0.6f, fadeOutDuration * 0.6f).SetEase(Ease.InBack)
                .OnComplete(() => bar.gameObject.SetActive(false));
        }

        if (timerBackground != null)
            timerBackground.DOColor(timerNormalColor, fadeOutDuration);
    }

    private void RefreshBar(LevelTimer timer)
    {
        float remaining = Mathf.Max(0f, timer.FreezeRemaining);
        float normalized = timer.FreezeDuration > 0f ? remaining / timer.FreezeDuration : 0f;

        if (barFill != null)
            barFill.anchorMax = new Vector2(Mathf.Clamp01(normalized), barFill.anchorMax.y);

        int seconds = Mathf.CeilToInt(remaining);
        if (secondsText != null && seconds != shownSeconds)
        {
            shownSeconds = seconds;
            secondsText.SetText("{0}s", seconds);
        }
    }

    private void AnimateSnowflakes()
    {
        float time = Time.time;
        for (int i = 0; i < snowflakes.Length; i++)
        {
            RectTransform flake = snowflakes[i];
            if (flake == null)
                continue;

            float phase = snowflakePhases[i];
            Vector2 origin = snowflakeOrigins[i];
            float fall = Mathf.Repeat(time * snowfallSpeed + phase * 90f, SnowfallDistance);
            float sway = Mathf.Sin(time * 1.3f + phase) * 18f;

            flake.anchoredPosition = origin + new Vector2(sway, -fall);
            flake.localRotation = Quaternion.Euler(0f, 0f, time * 25f + phase * 40f);

            // Fade in at the top of the loop and out at the bottom so the wrap is invisible.
            if (snowflakeGraphics[i] != null)
            {
                Color color = snowflakeGraphics[i].color;
                color.a = Mathf.Sin(fall / SnowfallDistance * Mathf.PI) * 0.9f;
                snowflakeGraphics[i].color = color;
            }
        }
    }

    private void SetHiddenImmediate()
    {
        shown = false;

        if (overlay != null)
        {
            overlay.alpha = 0f;
            overlay.blocksRaycasts = false;
            overlay.gameObject.SetActive(false);
        }

        if (bar != null)
        {
            bar.alpha = 0f;
            bar.blocksRaycasts = false;
            bar.gameObject.SetActive(false);
        }

        if (timerBackground != null)
            timerBackground.color = timerNormalColor;
    }

    private void KillTweens()
    {
        if (overlay != null) overlay.DOKill();
        if (overlayRect != null) overlayRect.DOKill();
        if (bar != null)
        {
            bar.DOKill();
            bar.transform.DOKill();
        }
        if (timerBackground != null) timerBackground.DOKill();
    }
}
