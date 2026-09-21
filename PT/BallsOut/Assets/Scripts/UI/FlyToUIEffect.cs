using System;
using System.Collections;
using DG.Tweening;
using UnityEngine;

public sealed class FlyToUIEffect : MonoBehaviour
{
    [SerializeField] private RectTransform target;
    [SerializeField, Min(0f)] private float completionDelay = 0.25f;

    private Coroutine routine;
    private Action pendingCompletion;

    public void Play(Action onComplete)
    {
        CompletePending();

        if (target == null || completionDelay <= 0f)
        {
            onComplete?.Invoke();
            return;
        }

        pendingCompletion = onComplete;
        target.DOKill();
        target.DOPunchScale(Vector3.one * 0.15f, completionDelay, 3)
            .SetUpdate(true);
        routine = StartCoroutine(CompleteAfterDelay());
    }

    private IEnumerator CompleteAfterDelay()
    {
        yield return new WaitForSecondsRealtime(completionDelay);
        routine = null;
        CompletePending();
    }

    private void OnDisable()
    {
        if (target != null)
            target.DOKill();

        CompletePending();
    }

    private void CompletePending()
    {
        if (routine != null)
        {
            StopCoroutine(routine);
            routine = null;
        }

        Action callback = pendingCompletion;
        pendingCompletion = null;
        callback?.Invoke();
    }
}
