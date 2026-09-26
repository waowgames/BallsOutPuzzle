using System;
using UnityEngine;

namespace BallsOut
{
    // Gold key on the lid of a box that opens a padlock. It bobs gently until its box
    // completes, then flies to the padlock, slides into the keyhole and turns.
    internal sealed class BoxKeyVisual : MonoBehaviour
    {
        private const float LiftTime = 0.22f;
        private const float FlyTime = 0.55f;
        private const float InsertTime = 0.2f;
        private const float TurnTime = 0.2f;

        private Transform model;
        private float cellSize;
        private float phase;
        private float elapsed = -1f;
        private BoxLockVisual target;
        private Action arrived;
        private Vector3 startPosition;
        private Quaternion startRotation;
        private Vector3 startScale;

        internal static BoxKeyVisual Create(BoxController box, float cellSize, float top)
        {
            Vector2 center = BoxLockVisual.FootprintCenter(box.Shape);
            var root = new GameObject("Key");
            root.transform.SetParent(box.transform, false);
            root.transform.localPosition = new Vector3(center.x * cellSize, top + cellSize * 0.04f, center.y * cellSize - cellSize * 0.04f);
            var key = root.AddComponent<BoxKeyVisual>();
            key.cellSize = cellSize;
            key.phase = (center.x * 1.7f + center.y) % 6.28f;
            key.model = LockKeyModels.CreatePart("Key Model", root.transform, LockKeyModels.Key, LockKeyModels.GoldMaterial).transform;
            key.model.localRotation = Quaternion.Euler(-BoxLockVisual.Tilt, 0f, 0f);
            key.model.localScale = Vector3.one * (cellSize * 1.35f);
            return key;
        }

        // Leaves the box for the padlock; `space` outlives the box (the board content root).
        internal void Fly(BoxLockVisual lockVisual, Transform space, Action onArrived)
        {
            target = lockVisual;
            arrived = onArrived;
            transform.SetParent(space, true);
            startPosition = transform.position;
            startRotation = model.rotation;
            startScale = model.localScale;
            elapsed = 0f;
        }

        private void Update()
        {
            float dt = Mathf.Min(Time.deltaTime, 1f / 20f);
            if (elapsed < 0f)
            {
                // Idle: a slow sway so the key reads as something to collect.
                phase += dt * 2.2f;
                model.localRotation = Quaternion.Euler(-BoxLockVisual.Tilt, Mathf.Sin(phase) * 7f, 0f);
                return;
            }
            if (target == null)
            {
                // The padlock went away (level reset): deliver anyway so the count stays honest.
                Arrive();
                return;
            }
            elapsed += dt;
            float world = cellSize * (transform.parent != null ? transform.parent.lossyScale.x : 1f);
            Quaternion face = target.FaceRotation;
            Vector3 up = face * Vector3.up;
            // Standing in the keyhole: the shaft points into the padlock face.
            Quaternion inserted = face * Quaternion.Euler(0f, 0f, -90f);
            float shaft = LockKeyModels.KeyTip * startScale.x * (transform.parent != null ? transform.parent.lossyScale.x : 1f);
            Vector3 seated = target.KeyholePosition + up * (shaft * 0.35f);
            Vector3 hover = seated + up * (world * 0.45f);
            Vector3 lifted = startPosition + Vector3.up * (world * 0.45f);

            float t = elapsed;
            if (t < LiftTime)
            {
                float k = EaseOut(t / LiftTime);
                transform.position = Vector3.LerpUnclamped(startPosition, lifted, k);
                model.localScale = startScale * (1f + 0.3f * k);
                return;
            }
            t -= LiftTime;
            if (t < FlyTime)
            {
                float k = Smooth(t / FlyTime);
                Vector3 control = (lifted + hover) * 0.5f + Vector3.up * (world * 1.3f);
                transform.position = (1f - k) * (1f - k) * lifted + 2f * (1f - k) * k * control + k * k * hover;
                Quaternion spin = Quaternion.AngleAxis(360f * k, Vector3.up) * startRotation;
                model.rotation = Quaternion.Slerp(spin, inserted, k);
                model.localScale = startScale * Mathf.Lerp(1.3f, 1f, k);
                return;
            }
            t -= FlyTime;
            if (t < InsertTime)
            {
                float k = EaseIn(t / InsertTime);
                transform.position = Vector3.Lerp(hover, seated, k);
                model.rotation = inserted;
                return;
            }
            t -= InsertTime;
            float turn = Smooth(t / TurnTime);
            transform.position = seated;
            model.rotation = face * Quaternion.AngleAxis(90f * turn, Vector3.up) * Quaternion.Euler(0f, 0f, -90f);
            if (t >= TurnTime) Arrive();
        }

        private void Arrive()
        {
            Action callback = arrived;
            arrived = null;
            Destroy(gameObject);
            callback?.Invoke();
        }

        private static float Smooth(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }

        private static float EaseOut(float t) => 1f - (1f - Mathf.Clamp01(t)) * (1f - Mathf.Clamp01(t));
        private static float EaseIn(float t) => Mathf.Clamp01(t) * Mathf.Clamp01(t);
    }
}
