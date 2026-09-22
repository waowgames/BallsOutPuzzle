using UnityEngine;
using UnityEngine.Events;

namespace BallsOut
{
    // Optional component on a user-provided box visual; Inspector events can
    // trigger an Animator, lid, particles or audio without coupling gameplay to art.
    public sealed class BoxCompletionAnimation : MonoBehaviour
    {
        [Min(0f)] public float duration = 0.25f;
        [SerializeField] private GameObject closedVisual;
        [SerializeField] private UnityEvent onCompletionStarted = new UnityEvent();
        [SerializeField] private UnityEvent onCompletionFinished = new UnityEvent();

        internal void Begin()
        {
            if (closedVisual != null) closedVisual.SetActive(true);
            onCompletionStarted.Invoke();
        }

        internal void Finish() => onCompletionFinished.Invoke();
    }
}
