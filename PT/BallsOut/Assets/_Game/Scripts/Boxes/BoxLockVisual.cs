using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

namespace BallsOut
{
    // Padlock over a locked box: counter badge, punch on each key, a shake when grabbed,
    // and a shackle-pop unlock. Pure presentation; BoxController owns the key count.
    internal sealed class BoxLockVisual : MonoBehaviour
    {
        // Leans the flat model toward the 65 degree board camera.
        internal const float Tilt = 22f;
        private const float PunchDuration = 0.32f;
        private const float ShakeDuration = 0.36f;
        private const float OpenDuration = 0.8f;
        private static Material labelMaterial;

        private Transform model;
        private Transform shackle;
        private Transform badge;
        private TextMeshPro label;
        private Vector3 shackleRest;
        private float modelScale;
        private float cellSize;
        private float punch = -1f;
        private float shake = -1f;
        private float open = -1f;
        private bool burst;

        internal Vector3 KeyholePosition => model.TransformPoint(LockKeyModels.Keyhole);
        internal Quaternion FaceRotation => model.rotation;
        internal bool IsOpening => open >= 0f;

        internal static BoxLockVisual Create(BoxController box, float cellSize, float top, int keys)
        {
            Vector2 center = FootprintCenter(box.Shape);
            Vector2 min = Vector2.positiveInfinity, max = Vector2.negativeInfinity;
            foreach (Vector2Int cell in box.Shape.Cells)
            {
                min = Vector2.Min(min, cell);
                max = Vector2.Max(max, cell);
            }
            // Big boxes get a big padlock; a single row or column keeps it inside the rim.
            float size = Mathf.Min(max.x - min.x, max.y - min.y) >= 1f ? 1.3f : 0.95f;

            var root = new GameObject("Padlock");
            root.transform.SetParent(box.transform, false);
            // The tilt dips the back of the lock; lift it so the shackle clears the rim.
            root.transform.localPosition = new Vector3(center.x * cellSize, top + cellSize * 0.1f * size, center.y * cellSize);
            var visual = root.AddComponent<BoxLockVisual>();
            visual.Build(cellSize, size, keys);
            return visual;
        }

        private void Build(float size, float scale, int keys)
        {
            cellSize = size;
            modelScale = size * scale;
            model = new GameObject("Model").transform;
            model.SetParent(transform, false);
            model.localRotation = Quaternion.Euler(-Tilt, 0f, 0f);
            model.localScale = Vector3.one * modelScale;
            // The shackle rises above the body: centre the whole outline on the anchor.
            var body = LockKeyModels.CreatePart("Body", model, LockKeyModels.Body,
                LockKeyModels.GoldMaterial, LockKeyModels.InkMaterial).transform;
            body.localPosition = Vector3.back * 0.08f;
            shackle = LockKeyModels.CreatePart("Shackle", model, LockKeyModels.Shackle, LockKeyModels.PaleGoldMaterial).transform;
            shackleRest = new Vector3(-LockKeyModels.ShackleSpan, LockKeyModels.ShackleHeight, LockKeyModels.ShackleBase - 0.08f);
            shackle.localPosition = shackleRest;
            badge = LockKeyModels.CreatePart("Counter", model, LockKeyModels.CounterBadge,
                LockKeyModels.WhiteMaterial, LockKeyModels.InkMaterial).transform;
            badge.localPosition = LockKeyModels.Badge + Vector3.back * 0.08f;
            CreateLabel();
            label.text = keys.ToString();
        }

        private void CreateLabel()
        {
            var labelObject = new GameObject("Counter Label");
            labelObject.transform.SetParent(badge, false);
            labelObject.transform.localPosition = Vector3.up * 0.006f;
            labelObject.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            label = labelObject.AddComponent<TextMeshPro>();
            MeshRenderer renderer = label.GetComponent<MeshRenderer>();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            label.font = TMP_Settings.defaultFontAsset;
            label.fontSize = 36f;
            label.fontStyle = FontStyles.Bold;
            label.isOrthographic = true;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Overflow;
            label.alignment = TextAlignmentOptions.Center;
            label.color = Color.white;
            if (labelMaterial == null && label.fontSharedMaterial != null)
            {
                labelMaterial = new Material(label.fontSharedMaterial) { name = "Padlock Counter" };
                if (labelMaterial.HasProperty("_OutlineWidth"))
                {
                    labelMaterial.EnableKeyword("OUTLINE_ON");
                    labelMaterial.SetFloat("_OutlineWidth", 0.18f);
                    labelMaterial.SetColor("_OutlineColor", new Color(0.16f, 0.1f, 0.2f));
                }
            }
            if (labelMaterial != null) label.fontSharedMaterial = labelMaterial;
            Vector2 preferred = label.GetPreferredValues("8");
            label.rectTransform.sizeDelta = preferred * 2f;
            float scale = preferred.y > 0f ? 0.13f / preferred.y : 0.01f;
            labelObject.transform.localScale = Vector3.one * scale;
        }

        internal void SetCount(int count)
        {
            if (open >= 0f) return;
            if (count <= 0)
            {
                open = 0f;
                punch = shake = -1f;
                return;
            }
            label.text = count.ToString();
            punch = 0f;
        }

        // The player tried to drag a locked box.
        internal void Nudge()
        {
            if (open < 0f && shake < 0f) shake = 0f;
        }

        private void Update()
        {
            float dt = Mathf.Min(Time.deltaTime, 1f / 20f);
            if (punch >= 0f)
            {
                punch += dt;
                float k = Mathf.Clamp01(punch / PunchDuration);
                float swell = Mathf.Sin(k * Mathf.PI);
                model.localScale = Vector3.one * (modelScale * (1f + swell * 0.14f));
                badge.localScale = Vector3.one * (1f + swell * 0.45f);
                if (k >= 1f) punch = -1f;
            }
            if (shake >= 0f)
            {
                shake += dt;
                float k = Mathf.Clamp01(shake / ShakeDuration);
                float wobble = Mathf.Sin(shake * 55f) * 14f * (1f - k);
                model.localRotation = Quaternion.Euler(-Tilt, 0f, wobble);
                if (k >= 1f) { shake = -1f; model.localRotation = Quaternion.Euler(-Tilt, 0f, 0f); }
            }
            if (open >= 0f) AdvanceOpen(dt);
        }

        // The shackle springs up and swings aside, then the padlock pops away in a gold burst.
        private void AdvanceOpen(float dt)
        {
            open += dt;
            float t = Mathf.Clamp01(open / OpenDuration);
            badge.localScale = Vector3.one * (1f - Smooth(t / 0.15f));
            float lift = BackOut(Mathf.Clamp01(t / 0.28f));
            shackle.localPosition = shackleRest + Vector3.forward * (0.1f * lift);
            shackle.localRotation = Quaternion.Euler(0f, -38f * Smooth((t - 0.18f) / 0.2f), 0f);
            float pop = Mathf.Clamp01((t - 0.42f) / 0.58f);
            float scale = pop < 0.3f ? 1f + Mathf.Sin(pop / 0.3f * Mathf.PI * 0.5f) * 0.22f : 1.22f * (1f - Smooth((pop - 0.3f) / 0.7f));
            model.localScale = Vector3.one * (modelScale * scale);
            model.localPosition = Vector3.up * (pop * pop * cellSize * 0.35f);
            model.localRotation = Quaternion.Euler(-Tilt, pop * pop * 160f, 0f);
            if (!burst && t >= 0.5f)
            {
                burst = true;
                LockKeyEffects.Burst(transform.parent != null ? transform.parent.parent : null, model.position,
                    cellSize * transform.lossyScale.x, 22);
            }
            if (t >= 1f) Destroy(gameObject);
        }

        internal static Vector2 FootprintCenter(BoxShapeDefinition shape)
        {
            // The footprint centre when it lies on the box (bars, squares), else the nearest cell.
            Vector2 center = Vector2.zero;
            foreach (Vector2Int cell in shape.Cells) center += cell;
            center /= shape.Cells.Count;
            Vector2Int nearest = shape.Cells[0];
            float best = float.MaxValue;
            foreach (Vector2Int cell in shape.Cells)
            {
                Vector2 delta = center - cell;
                if (Mathf.Abs(delta.x) <= 0.5f && Mathf.Abs(delta.y) <= 0.5f) return center;
                if (delta.sqrMagnitude < best) { best = delta.sqrMagnitude; nearest = cell; }
            }
            return nearest;
        }

        private static float Smooth(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }

        private static float BackOut(float t)
        {
            const float s = 1.9f;
            t -= 1f;
            return t * t * ((s + 1f) * t + s) + 1f;
        }
    }
}
