using UnityEngine;

namespace Voodoo.Tiny.Sauce.Privacy
{
    /// <summary>
    /// Fits a RectTransform to <see cref="Screen.safeArea"/> so consent UI stays clear of
    /// system bars / cutouts (required on Android 16+ where edge-to-edge is mandatory).
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class PrivacySafeAreaFitter : MonoBehaviour
    {
        private RectTransform _rectTransform;
        private Rect _lastSafeArea;
        private int _lastScreenWidth;
        private int _lastScreenHeight;

        private void Awake()
        {
            _rectTransform = (RectTransform)transform;
            ApplySafeArea();
        }

        private void OnEnable()
        {
            ApplySafeArea();
        }

        private void Update()
        {
            if (Screen.safeArea != _lastSafeArea
                || Screen.width != _lastScreenWidth
                || Screen.height != _lastScreenHeight)
            {
                ApplySafeArea();
            }
        }

        private void ApplySafeArea()
        {
            if (_rectTransform == null)
                _rectTransform = (RectTransform)transform;

            var safeArea = Screen.safeArea;
            _lastSafeArea = safeArea;
            _lastScreenWidth = Screen.width;
            _lastScreenHeight = Screen.height;

            if (_lastScreenWidth <= 0 || _lastScreenHeight <= 0)
                return;

            var anchorMin = safeArea.position;
            var anchorMax = safeArea.position + safeArea.size;
            anchorMin.x /= _lastScreenWidth;
            anchorMin.y /= _lastScreenHeight;
            anchorMax.x /= _lastScreenWidth;
            anchorMax.y /= _lastScreenHeight;

            _rectTransform.anchorMin = anchorMin;
            _rectTransform.anchorMax = anchorMax;
            _rectTransform.offsetMin = Vector2.zero;
            _rectTransform.offsetMax = Vector2.zero;
        }

        /// <summary>
        /// Ensures a safe-area container exists under <paramref name="canvasRoot"/> and
        /// reparents interactive panels into it. Full-screen blockers should stay as
        /// siblings of the container so dimming still covers the whole display.
        /// </summary>
        public static RectTransform EnsureContainer(RectTransform canvasRoot, params GameObject[] panelsToContain)
        {
            if (canvasRoot == null)
                return null;

            const string containerName = "SafeArea";
            Transform existing = canvasRoot.Find(containerName);
            RectTransform container;

            if (existing != null)
            {
                container = (RectTransform)existing;
            }
            else
            {
                var containerObject = new GameObject(containerName, typeof(RectTransform));
                container = (RectTransform)containerObject.transform;
                container.SetParent(canvasRoot, false);
                container.localScale = Vector3.one;
                containerObject.layer = canvasRoot.gameObject.layer;
                containerObject.AddComponent<PrivacySafeAreaFitter>();
            }

            if (panelsToContain == null)
                return container;

            for (int i = 0; i < panelsToContain.Length; i++)
            {
                GameObject panel = panelsToContain[i];
                if (panel == null)
                    continue;

                Transform panelTransform = panel.transform;
                if (panelTransform.parent == container)
                    continue;

                panelTransform.SetParent(container, false);
            }

            return container;
        }
    }
}
