using System.Collections;
using UnityEngine;

[RequireComponent(typeof(RectTransform))]
public class MouseFollowHandOverlay : MonoBehaviour
{
    [Header("Follow")]
    [SerializeField] private Canvas targetCanvas;
    [SerializeField] private Vector2 screenOffset = new Vector2(56f, -40f);
    [SerializeField] [Min(0f)] private float followSharpness = 18f;
    [SerializeField] private bool useUnscaledTime = true;

    [Header("Click Scale")]
    [SerializeField] [Min(0.1f)] private float pressedScaleMultiplier = 0.86f;
    [SerializeField] [Min(0.1f)] private float releaseScaleMultiplier = 1.05f;
    [SerializeField] [Min(0.01f)] private float pressDuration = 0.05f;
    [SerializeField] [Min(0.01f)] private float releaseDuration = 0.12f;
    [SerializeField] [Min(0.01f)] private float settleDuration = 0.08f;

    private RectTransform _rectTransform;
    private RectTransform _canvasRectTransform;
    private Vector2 _smoothedAnchoredPosition;
    private Vector3 _baseScale;
    private Coroutine _clickRoutine;
    private bool _hasInitialPosition;

    private void Awake()
    {
        CacheReferences();
    }

    private void OnEnable()
    {
        CacheReferences();
        _baseScale = _rectTransform.localScale;
        SnapToMouse();
    }

    private void CacheReferences()
    {
        if (_rectTransform == null)
            _rectTransform = GetComponent<RectTransform>();

        if (targetCanvas == null)
            targetCanvas = GetComponentInParent<Canvas>();

        if (targetCanvas != null)
            _canvasRectTransform = targetCanvas.transform as RectTransform;
    }

    private void LateUpdate()
    {
        if (_canvasRectTransform == null)
            return;

        FollowMouse();

        if (Input.GetMouseButtonDown(0))
            PlayClickEffect();
    }

    private void SnapToMouse()
    {
        if (_canvasRectTransform == null)
            return;

        Vector2 targetPosition = GetMouseAnchoredPosition();
        _smoothedAnchoredPosition = targetPosition;
        _rectTransform.anchoredPosition = targetPosition;
        _hasInitialPosition = true;
    }

    private void FollowMouse()
    {
        Vector2 targetPosition = GetMouseAnchoredPosition();

        if (!_hasInitialPosition)
        {
            _smoothedAnchoredPosition = targetPosition;
            _hasInitialPosition = true;
        }

        float deltaTime = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        float lerpFactor = followSharpness <= 0f
            ? 1f
            : 1f - Mathf.Exp(-followSharpness * deltaTime);

        _smoothedAnchoredPosition = Vector2.Lerp(_smoothedAnchoredPosition, targetPosition, lerpFactor);
        _rectTransform.anchoredPosition = _smoothedAnchoredPosition;
    }

    private Vector2 GetMouseAnchoredPosition()
    {
        Vector2 screenPoint = (Vector2)Input.mousePosition + screenOffset;
        Camera eventCamera = targetCanvas != null && targetCanvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? targetCanvas.worldCamera != null ? targetCanvas.worldCamera : Camera.main
            : null;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _canvasRectTransform,
            screenPoint,
            eventCamera,
            out Vector2 localPoint);

        return localPoint;
    }

    private void PlayClickEffect()
    {
        if (_clickRoutine != null)
            StopCoroutine(_clickRoutine);

        _clickRoutine = StartCoroutine(AnimateClickScale());
    }

    private IEnumerator AnimateClickScale()
    {
        yield return AnimateScale(_baseScale, _baseScale * pressedScaleMultiplier, pressDuration);
        yield return AnimateScale(_baseScale * pressedScaleMultiplier, _baseScale * releaseScaleMultiplier, releaseDuration);
        yield return AnimateScale(_baseScale * releaseScaleMultiplier, _baseScale, settleDuration);
        _clickRoutine = null;
    }

    private IEnumerator AnimateScale(Vector3 from, Vector3 to, float duration)
    {
        if (duration <= 0f)
        {
            _rectTransform.localScale = to;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            t = 1f - Mathf.Pow(1f - t, 3f);
            _rectTransform.localScale = Vector3.LerpUnclamped(from, to, t);
            yield return null;
        }

        _rectTransform.localScale = to;
    }
}
