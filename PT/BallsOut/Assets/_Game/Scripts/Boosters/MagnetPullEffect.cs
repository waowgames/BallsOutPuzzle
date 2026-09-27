using UnityEngine;

namespace BallsOut
{
    /// <summary>
    /// A magnet that pops up over the picked box, faces the camera and shakes with effort while the
    /// balls stream in, then shrinks away.
    /// </summary>
    public sealed class MagnetPullEffect : MonoBehaviour
    {
        private const float Duration = 1.1f;
        private const float AppearTime = 0.18f;
        private const float LeaveTime = 0.22f;

        private float size;
        private float elapsed;

        public static void Play(Sprite icon, BoxController box)
        {
            if (icon == null || box == null) return;
            float cell = box.runtimeCellSize;
            Vector3 center = Vector3.zero;
            foreach (Vector2Int c in box.Shape.Cells) center += new Vector3(c.x, 0f, c.y);
            center *= cell / box.Shape.CellCount;
            center.y = box.ArtTop + cell * 0.55f;
            var go = new GameObject("Magnet Pull");
            go.transform.SetParent(box.transform.parent != null ? box.transform.parent : box.transform, false);
            go.transform.position = box.transform.TransformPoint(center);
            SpriteRenderer sprite = go.AddComponent<SpriteRenderer>();
            sprite.sprite = icon;
            sprite.sortingOrder = 100;
            var effect = go.AddComponent<MagnetPullEffect>();
            effect.size = cell * 0.9f / Mathf.Max(0.01f, icon.bounds.size.y);
            effect.Evaluate();
        }

        private void LateUpdate()
        {
            elapsed += Time.deltaTime;
            Evaluate();
            if (elapsed >= Duration) Destroy(gameObject);
        }

        private void Evaluate()
        {
            Camera camera = Camera.main;
            if (camera != null) transform.rotation = camera.transform.rotation;
            float scale = 1f;
            if (elapsed < AppearTime)
            {
                float k = elapsed / AppearTime;
                scale = 1f + 0.25f * Mathf.Sin(k * Mathf.PI) - (1f - k) * (1f - k);
            }
            else if (elapsed > Duration - LeaveTime)
            {
                float k = (elapsed - (Duration - LeaveTime)) / LeaveTime;
                scale = 1f - k * k;
            }
            float wobble = Mathf.Sin(elapsed * 40f) * 8f * Mathf.Clamp01(elapsed / AppearTime);
            transform.rotation *= Quaternion.Euler(0f, 0f, wobble);
            transform.localScale = Vector3.one * size * Mathf.Max(0f, scale);
        }
    }
}
