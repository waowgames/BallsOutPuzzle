using UnityEngine;
using DG.Tweening;

/// <summary>
/// Abstract base class for stackable UI popups (Win, Fail, Pause, Tutorial, Booster).
/// Manages CanvasGroup fade, input blocking, and UIManager stack integration.
/// Subclasses override <see cref="OnShow"/> / <see cref="OnHide"/> for custom logic.
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public abstract class UIPopup : MonoBehaviour
{
    [Header("UIPopup Settings")]
    [SerializeField] protected float fadeTime = 0.2f;

    /// <summary>Unique identifier for this popup. Override in subclass.</summary>
    public virtual string PopupId => GetType().Name;

    /// <summary>Whether the popup is currently showing.</summary>
    public bool IsShowing { get; protected set; }

    protected CanvasGroup canvasGroup;
    protected Canvas canvas;

    protected virtual void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        canvas = GetComponent<Canvas>();

        // Start hidden
        SetHiddenImmediate();
    }

    /// <summary>
    /// Shows the popup with a fade-in animation.
    /// Pushes onto UIManager's popup stack if available.
    /// </summary>
    public virtual void Show()
    {
        if (IsShowing) return;
        IsShowing = true;

        if (canvas != null) canvas.enabled = true;

        canvasGroup.DOKill();
        canvasGroup.interactable = true;
        canvasGroup.blocksRaycasts = true;
        canvasGroup.DOFade(1f, fadeTime).SetUpdate(true);

        OnShow();
        GameEvents.RaisePopupOpened(PopupId);

        // Push onto UIManager stack
        if (UIManager.Instance != null)
            UIManager.Instance.PushPopup(this);
    }

    /// <summary>
    /// Hides the popup with a fade-out animation.
    /// Removes from UIManager's popup stack if available.
    /// </summary>
    public virtual void Hide()
    {
        if (!IsShowing) return;
        IsShowing = false;

        canvasGroup.DOKill();
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
        canvasGroup.DOFade(0f, fadeTime).SetUpdate(true)
            .OnComplete(() =>
            {
                if (canvas != null) canvas.enabled = false;
            });

        OnHide();
        GameEvents.RaisePopupClosed(PopupId);

        // Remove from UIManager stack
        if (UIManager.Instance != null)
            UIManager.Instance.RemovePopup(this);
    }

    /// <summary>Called after the popup starts showing. Override for custom setup.</summary>
    protected virtual void OnShow() { }

    /// <summary>Called after the popup starts hiding. Override for custom teardown.</summary>
    protected virtual void OnHide() { }

    /// <summary>Immediately hides without animation.</summary>
    protected void SetHiddenImmediate()
    {
        IsShowing = false;
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }
        if (canvas != null) canvas.enabled = false;
    }
}
