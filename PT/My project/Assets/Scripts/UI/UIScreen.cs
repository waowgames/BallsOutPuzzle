using UnityEngine;
using DG.Tweening;

/// <summary>
/// Abstract base class for full-screen UI panels (HUD, Settings, Shop).
/// Subclasses override <see cref="OnShow"/> / <see cref="OnHide"/> for custom logic.
/// Show/Hide uses CanvasGroup fade via DOTween.
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public abstract class UIScreen : MonoBehaviour
{
    [Header("UIScreen Settings")]
    [SerializeField] protected float fadeTime = 0.25f;

    /// <summary>Unique identifier for this screen. Override in subclass.</summary>
    public virtual string ScreenId => GetType().Name;

    /// <summary>Whether the screen is currently visible.</summary>
    public bool IsVisible { get; private set; }

    protected CanvasGroup canvasGroup;
    protected Canvas canvas;

    protected virtual void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        canvas = GetComponent<Canvas>();

        // Start hidden
        SetVisibleImmediate(false);
    }

    /// <summary>
    /// Shows the screen with a fade-in animation.
    /// </summary>
    public virtual void Show()
    {
        if (IsVisible) return;
        IsVisible = true;

        if (canvas != null) canvas.enabled = true;

        canvasGroup.DOKill();
        canvasGroup.interactable = true;
        canvasGroup.blocksRaycasts = true;
        canvasGroup.DOFade(1f, fadeTime).SetUpdate(true);

        OnShow();
        GameEvents.RaiseScreenOpened(ScreenId);
    }

    /// <summary>
    /// Hides the screen with a fade-out animation.
    /// </summary>
    public virtual void Hide()
    {
        if (!IsVisible) return;
        IsVisible = false;

        canvasGroup.DOKill();
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
        canvasGroup.DOFade(0f, fadeTime).SetUpdate(true)
            .OnComplete(() =>
            {
                if (canvas != null) canvas.enabled = false;
            });

        OnHide();
        GameEvents.RaiseScreenClosed(ScreenId);
    }

    /// <summary>Called after the screen starts showing. Override for custom setup.</summary>
    protected virtual void OnShow() { }

    /// <summary>Called after the screen starts hiding. Override for custom teardown.</summary>
    protected virtual void OnHide() { }

    /// <summary>Immediately sets visibility without animation.</summary>
    protected void SetVisibleImmediate(bool visible)
    {
        IsVisible = visible;

        if (canvasGroup != null)
        {
            canvasGroup.alpha = visible ? 1f : 0f;
            canvasGroup.interactable = visible;
            canvasGroup.blocksRaycasts = visible;
        }

        if (canvas != null) canvas.enabled = visible;
    }

    protected virtual void OnEnable()
    {
        // Auto-register with UIManager if available
        if (UIManager.Instance != null)
            UIManager.Instance.RegisterScreen(this);
    }

    protected virtual void OnDisable()
    {
        if (UIManager.Instance != null)
            UIManager.Instance.UnregisterScreen(this);
    }
}
