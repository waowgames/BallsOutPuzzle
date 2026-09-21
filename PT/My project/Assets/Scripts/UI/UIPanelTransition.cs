using System;
using DG.Tweening;
using UnityEngine;

/// <summary>
/// Animates explicitly assigned UI content roots without affecting their full-screen
/// canvas, backdrop, or individual child controls.
/// </summary>
public sealed class UIPanelTransition : MonoBehaviour
{
    [Header("Content")]
    [SerializeField] private RectTransform[] contentRoots = Array.Empty<RectTransform>();
    [SerializeField] private CanvasGroup backdrop;

    [Header("Behaviour")]
    [SerializeField] private bool playOnEnable;
    [SerializeField] private bool deactivateOwnerAfterClose;

    [Header("Timing")]
    [SerializeField, Min(0.01f)] private float openDuration = 0.20f;
    [SerializeField, Min(0.01f)] private float closeDuration = 0.14f;
    [SerializeField, Min(0f)] private float stagger = 0.04f;

    private Vector3[] _authoredScales;
    private Sequence _activeSequence;

    private void Awake()
    {
        CacheAuthoredScales();
    }

    private void OnEnable()
    {
        if (playOnEnable)
            PlayOpen();
    }

    private void OnDisable()
    {
        KillActiveSequence();
    }

    private void OnDestroy()
    {
        KillActiveSequence();
    }

    /// <summary>Plays the configured content roots from zero to their authored scale.</summary>
    public Tween PlayOpen()
    {
        CacheAuthoredScalesIfNeeded();
        KillActiveSequence();

        _activeSequence = DOTween.Sequence().SetUpdate(true);

        if (backdrop != null)
        {
            backdrop.DOKill(false);
            _activeSequence.Join(backdrop.DOFade(1f, openDuration));
        }

        bool hasPreviousRoot = false;
        for (int i = 0; i < contentRoots.Length; i++)
        {
            RectTransform root = contentRoots[i];
            if (root == null)
                continue;

            root.DOKill(false);
            root.localScale = Vector3.zero;

            if (hasPreviousRoot && stagger > 0f)
                _activeSequence.AppendInterval(stagger);

            _activeSequence.Join(root.DOScale(_authoredScales[i], openDuration)
                .SetEase(Ease.OutBack));
            hasPreviousRoot = true;
        }

        _activeSequence.OnComplete(ClearCompletedSequence);
        return _activeSequence;
    }

    /// <summary>Scales configured content roots out and invokes the callback on completion.</summary>
    public Tween PlayClose(Action onComplete)
    {
        CacheAuthoredScalesIfNeeded();
        KillActiveSequence();

        _activeSequence = DOTween.Sequence().SetUpdate(true);

        if (backdrop != null)
        {
            backdrop.DOKill(false);
            _activeSequence.Join(backdrop.DOFade(0f, closeDuration));
        }

        bool hasPreviousRoot = false;
        for (int i = contentRoots.Length - 1; i >= 0; i--)
        {
            RectTransform root = contentRoots[i];
            if (root == null)
                continue;

            root.DOKill(false);

            if (hasPreviousRoot && stagger > 0f)
                _activeSequence.AppendInterval(stagger);

            _activeSequence.Join(root.DOScale(Vector3.zero, closeDuration)
                .SetEase(Ease.InBack));
            hasPreviousRoot = true;
        }

        _activeSequence.OnComplete(() =>
        {
            _activeSequence = null;
            onComplete?.Invoke();
        });
        return _activeSequence;
    }

    /// <summary>Plays the close animation before deactivating its owner when configured.</summary>
    public void CloseAndDeactivate()
    {
        PlayClose(() =>
        {
            if (deactivateOwnerAfterClose)
                gameObject.SetActive(false);
        });
    }

    /// <summary>
    /// Configures the roots animated by this transition at runtime.
    /// Intended for existing UI prefabs that do not yet contain a transition component.
    /// </summary>
    public void SetContentRoots(RectTransform[] roots)
    {
        contentRoots = roots ?? Array.Empty<RectTransform>();
        CacheAuthoredScales();
    }

    private void CacheAuthoredScalesIfNeeded()
    {
        if (_authoredScales == null || _authoredScales.Length != contentRoots.Length)
            CacheAuthoredScales();
    }

    private void CacheAuthoredScales()
    {
        _authoredScales = new Vector3[contentRoots.Length];
        for (int i = 0; i < contentRoots.Length; i++)
        {
            RectTransform root = contentRoots[i];
            _authoredScales[i] = root != null ? root.localScale : Vector3.one;
        }
    }

    private void KillActiveSequence()
    {
        if (_activeSequence == null)
            return;

        _activeSequence.Kill(false);
        _activeSequence = null;
    }

    private void ClearCompletedSequence()
    {
        _activeSequence = null;
    }
}
